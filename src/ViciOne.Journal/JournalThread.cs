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
    private readonly JournalOpeningMode _openingMode;
    private readonly ILogger<SystemdJournal>? _logger;
    private readonly TaskCompletionSource _initializationTcs;
#pragma warning disable CA2213 // _journal is disposed in ThreadLoop's finally block on the dedicated thread
    private SystemdJournal? _journal;
#pragma warning restore CA2213
    private int _disposed;

    internal JournalThread(JournalOptions options, ILogger<SystemdJournal>? logger = null)
    {
        _openingMode = options.OpeningMode;
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
            _journal = new SystemdJournal(_logger, _openingMode);
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
    internal void Execute(Action<SystemdJournal> action, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        var workItem = new ActionWorkItem(action);

        try
        {
            _workQueue.Add(workItem, cancellationToken);
        }
        catch (InvalidOperationException) when (_workQueue.IsAddingCompleted)
        {
            throw new ObjectDisposedException(nameof(JournalThread), "The journal thread has been disposed.");
        }

        workItem.Wait(cancellationToken);
    }

    /// <summary>
    /// Executes a function on the dedicated journal thread and returns the result.
    /// </summary>
    /// <param name="func">The function to execute.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    internal T Execute<T>(Func<SystemdJournal, T> func, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        var workItem = new FuncWorkItem<T>(func);

        try
        {
            _workQueue.Add(workItem, cancellationToken);
        }
        catch (InvalidOperationException) when (_workQueue.IsAddingCompleted)
        {
            throw new ObjectDisposedException(nameof(JournalThread), "The journal thread has been disposed.");
        }

        return workItem.GetResult(cancellationToken);
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
        public abstract void Execute(SystemdJournal journal);
        public abstract void SetException(Exception ex);
    }

    private sealed class ActionWorkItem : WorkItem
    {
        private readonly Action<SystemdJournal> _action;
        private readonly TaskCompletionSource _tcs;

        public ActionWorkItem(Action<SystemdJournal> action)
        {
            _action = action;
            _tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public override void Execute(SystemdJournal journal)
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
        private readonly Func<SystemdJournal, T> _func;
        private readonly TaskCompletionSource<T> _tcs;

        public FuncWorkItem(Func<SystemdJournal, T> func)
        {
            _func = func;
            _tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public override void Execute(SystemdJournal journal)
        {
            var result = _func(journal);
            _tcs.SetResult(result);
        }

        public override void SetException(Exception ex)
            => _tcs.TrySetException(ex);

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
