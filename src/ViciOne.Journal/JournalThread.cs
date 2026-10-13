using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ViciOne.Journal;

/// <summary>
/// Provides a dedicated thread for journal operations to ensure thread affinity
/// as required by the systemd sd-journal API.
/// </summary>
/// <remarks>
/// From sd-journal(3) man page:
/// "Functions that operate on sd_journal objects are thread agnostic —
/// given sd_journal pointer may only be used from one specific thread
/// at all times (and it has to be the very same one during the entire
/// lifetime of the object)"
/// </remarks>
internal sealed class JournalThread : IDisposable
{
    private static int s_threadCounter;

    private readonly Thread _thread;
    private readonly BlockingCollection<WorkItem> _workQueue;
    private readonly Func<ISystemdJournal> _openJournal;
    private readonly ILogger<SystemdJournal>? _logger;
    private readonly TaskCompletionSource _initializationTcs;
#pragma warning disable CA2213 // _journal is disposed in ThreadLoop's finally block on the dedicated thread
    private ISystemdJournal? _journal;
#pragma warning restore CA2213
    private int _disposed;

    internal JournalThread(Func<ISystemdJournal> openJournal, ILogger<SystemdJournal>? logger = null)
    {
        _openJournal = openJournal;
        _logger = logger;
        _workQueue = new BlockingCollection<WorkItem>();
        _initializationTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var threadId = Interlocked.Increment(ref s_threadCounter);
        _thread = new Thread(ThreadLoop)
        {
            Name = $"JournalThread-{threadId}",
            IsBackground = true
        };
        _thread.Start();

        // Wait for journal initialization to complete (success or failure)
        try
        {
            _initializationTcs.Task.GetAwaiter().GetResult();
        }
        catch
        {
            // Ensure cleanup if initialization failed
            _workQueue.CompleteAdding();
            _thread.Join(TimeSpan.FromSeconds(5));
            _workQueue.Dispose();
            throw;
        }
    }

    private void ThreadLoop()
    {
        try
        {
            // Create journal on this dedicated thread
            _journal = _openJournal();
            _initializationTcs.SetResult();
        }
        catch (Exception ex)
        {
            _initializationTcs.SetException(ex);
            // Drain any queued work items that were added before we could signal failure
            while (_workQueue.TryTake(out var workItem))
            {
                workItem.SetException(new InvalidOperationException("Journal initialization failed.", ex));
            }
            return;
        }

        try
        {
            foreach (var workItem in _workQueue.GetConsumingEnumerable())
            {
                try
                {
                    workItem.Execute(_journal);
                }
                catch (Exception ex)
                {
                    workItem.SetException(ex);
                }
            }
        }
        finally
        {
            _journal?.Dispose();
            _journal = null;
        }
    }

    /// <summary>
    /// Executes an action on the dedicated journal thread.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    internal void Execute(Action<ISystemdJournal> action, CancellationToken cancellationToken = default)
    {
        var workItem = new ActionWorkItem(action);
        Enqueue(workItem, cancellationToken);
        workItem.Wait(cancellationToken);
    }

    /// <summary>
    /// Executes a function on the dedicated journal thread and returns the result.
    /// </summary>
    /// <param name="func">The function to execute.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    internal T Execute<T>(Func<ISystemdJournal, T> func, CancellationToken cancellationToken = default)
    {
        var workItem = new FuncWorkItem<T>(func);
        Enqueue(workItem, cancellationToken);
        return workItem.GetResult(cancellationToken);
    }

    /// <summary>
    /// Executes a function on the dedicated journal thread without blocking the caller.
    /// </summary>
    /// <returns>A task that completes when the function has run; <paramref name="func"/> observes cancellation itself.</returns>
    internal Task<T> ExecuteAsync<T>(Func<ISystemdJournal, T> func)
    {
        var workItem = new FuncWorkItem<T>(func);
        Enqueue(workItem, CancellationToken.None);
        return workItem.Task;
    }

    private void Enqueue(WorkItem workItem, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        try
        {
            _workQueue.Add(workItem, cancellationToken);
        }
        catch (InvalidOperationException) when (_workQueue.IsAddingCompleted)
        {
            throw new ObjectDisposedException(nameof(JournalThread), "The journal thread has been disposed.");
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _workQueue.CompleteAdding();

        // Wait for thread to finish (with timeout to prevent hangs)
        if (!_thread.Join(TimeSpan.FromSeconds(5)))
        {
            _logger?.LogWarning("JournalThread did not terminate within timeout");
        }

        _workQueue.Dispose();
    }

    private abstract class WorkItem
    {
        public abstract void Execute(ISystemdJournal journal);
        public abstract void SetException(Exception ex);
    }

    private sealed class ActionWorkItem : WorkItem
    {
        private readonly Action<ISystemdJournal> _action;
        private readonly TaskCompletionSource _tcs;

        public ActionWorkItem(Action<ISystemdJournal> action)
        {
            _action = action;
            _tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public override void Execute(ISystemdJournal journal)
        {
            _action(journal);
            _tcs.SetResult();
        }

        public override void SetException(Exception ex)
            => _tcs.TrySetException(ex);

        public void Wait(CancellationToken cancellationToken)
        {
            try
            {
                _tcs.Task.Wait(cancellationToken);
            }
            catch (AggregateException ae) when (ae.InnerException is not null)
            {
                throw ae.InnerException;
            }
        }
    }

    private sealed class FuncWorkItem<T> : WorkItem
    {
        private readonly Func<ISystemdJournal, T> _func;
        private readonly TaskCompletionSource<T> _tcs;

        public FuncWorkItem(Func<ISystemdJournal, T> func)
        {
            _func = func;
            _tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public override void Execute(ISystemdJournal journal)
        {
            var result = _func(journal);
            _tcs.SetResult(result);
        }

        public override void SetException(Exception ex)
            => _tcs.TrySetException(ex);

        public Task<T> Task
            => _tcs.Task;

        public T GetResult(CancellationToken cancellationToken)
        {
            try
            {
                _tcs.Task.Wait(cancellationToken);
                return _tcs.Task.Result;
            }
            catch (AggregateException ae) when (ae.InnerException is not null)
            {
                throw ae.InnerException;
            }
        }
    }
}
