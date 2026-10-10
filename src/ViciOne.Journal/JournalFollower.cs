using Microsoft.Extensions.Logging;

namespace ViciOne.Journal;

/// <summary>
/// Reads the entries appended to the journal on every tick and hands them over in one batch per tick.
/// </summary>
internal sealed class JournalFollower<T> : IDisposable
{
    private readonly JournalThread _journalThread;
    private readonly Func<ISystemdJournal, T> _readEntry;
    private readonly Action<List<T>> _deliver;
    private readonly Action<Exception> _fail;
    private readonly Action _onDispose;
    private readonly ILogger<SystemdJournal>? _logger;
    private PeriodicTimer? _timer;
    private readonly TimeSpan _interval;
    private Task? _worker;
    private CancellationTokenSource? _workerCts;

    /// <param name="deliver">Receives the non-empty batch of each tick.</param>
    /// <param name="fail">Receives the error that stopped the follower.</param>
    internal JournalFollower(
        Func<ISystemdJournal> openJournal,
        TimeSpan interval,
        Func<ISystemdJournal, T> readEntry,
        Action<List<T>> deliver,
        Action<Exception> fail,
        Action onDispose,
        ILogger<SystemdJournal>? logger)
    {
        if (interval.TotalMilliseconds < 250)
            throw new InvalidOperationException("The interval must be greater then 250ms.");

        _journalThread = new JournalThread(openJournal, logger);
        _readEntry = readEntry;
        _deliver = deliver;
        _fail = fail;
        _onDispose = onDispose;
        _logger = logger;
        _interval = interval;

        try
        {
            _journalThread.Execute(j => j.SeekTail());
        }
        catch (Exception ex)
        {
            _journalThread.Dispose();
            throw new InvalidOperationException("Error seeking journal tail.", ex);
        }
    }

    private async Task ProcessInterval(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                // Collect all new entries on the dedicated thread
                var entries = _journalThread.Execute(j =>
                {
                    j.ProcessChanges();

                    var list = new List<T>();
                    while (j.Next())
                        list.Add(_readEntry(j));
                    return list;
                }, cancellationToken);

                // Invoke events on the current (ThreadPool) thread
                if (entries.Count > 0)
                    _deliver(entries);
            }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // Stop and Dispose cancel before they release the journal thread, so a failure after that, such as
            // handing work to the disposed thread, is part of shutting down and not an error of the session.
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing journal entries");
            _fail(ex);
        }
    }

    public void Start(int logCount)
    {
        if (_timer is null)
        {
            _timer = new(_interval);
            _workerCts = new CancellationTokenSource();

            GoBack(logCount);

            _worker = ProcessInterval(_timer, _workerCts.Token);
        }
    }

    public async Task Stop()
    {
        if (_timer is not null)
        {
            // Signal cancellation first
            _workerCts?.Cancel();
            _timer.Dispose();

            if (_worker is not null)
            {
                try
                {
                    await _worker.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected during cancellation
                }

                _worker = null;
            }

            _workerCts?.Dispose();
            _workerCts = null;
            _timer = null;
        }
    }

    public void SetFilter(string filter)
    {
        if (_timer is not null)
            throw new InvalidOperationException("Cannot change filter while session is running.");

        try
        {
            _journalThread.Execute(j =>
            {
                j.FlushMatches();
                Filter.Apply(filter, j);
                j.SeekTail();
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error applying filters. ({filter})", ex);
        }
    }

    private void GoBack(int times)
    {
        _journalThread.Execute(j =>
        {
            var moved = false;
            for (var i = 0; i < times && j.Previous(); i++)
                moved = true;

            // Next() would step past the entry reached last; seeking its cursor makes it the first one read.
            if (moved)
                j.SeekCursor(j.GetCursor());
        });
    }

    public void Dispose()
    {
        _workerCts?.Cancel();
        _timer?.Dispose();
        _timer = null;
        _workerCts?.Dispose();
        _workerCts = null;
        _journalThread.Dispose();
        _onDispose();
    }
}
