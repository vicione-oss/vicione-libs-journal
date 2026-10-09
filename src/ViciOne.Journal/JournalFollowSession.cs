using Microsoft.Extensions.Logging;

namespace ViciOne.Journal;

public sealed class JournalFollowSession : IJournalFollowSession
{
    private readonly JournalThread _journalThread;
    private readonly Action _onDispose;
    private readonly ILogger<SystemdJournal>? _logger;
    private PeriodicTimer? _timer;
    private readonly TimeSpan _interval;
    private Task? _worker;
    private CancellationTokenSource? _workerCts;

    /// <summary>
    /// Raised when a new journal entry is received.
    /// </summary>
    public event Action<IReadOnlyDictionary<string, string>>? NewEntry;

    /// <summary>
    /// Raised when an error occurs during journal processing.
    /// The session will stop after raising this event.
    /// </summary>
    public event Action<Exception>? Error;

    internal JournalFollowSession(Func<ISystemdJournal> openJournal, TimeSpan interval, Action onDispose, ILogger<SystemdJournal>? logger = null)
    {
        if (interval.TotalMilliseconds < 250)
            throw new InvalidOperationException("The interval must be greater then 250ms.");

        _journalThread = new JournalThread(openJournal, logger);
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
                    var list = new List<IReadOnlyDictionary<string, string>>();
                    while (j.Next())
                        list.Add(j.GetCurrentEntry());
                    return list;
                }, cancellationToken);

                // Invoke events on the current (ThreadPool) thread
                foreach (var entry in entries)
                    NewEntry?.Invoke(entry);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal cancellation, don't treat as error
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing journal entries");
            Error?.Invoke(ex);
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
            for (var i = 0; i < times; i++)
                j.Previous();
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
