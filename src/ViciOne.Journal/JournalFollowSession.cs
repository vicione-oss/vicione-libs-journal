using Microsoft.Extensions.Logging;

namespace ViciOne.Journal;

public sealed class JournalFollowSession : IDisposable
{
    private readonly SystemdJournal _journal;
    private readonly Action _onDispose;
    private PeriodicTimer? _timer;
    private readonly TimeSpan _interval;
    private Task? _worker;

    public event Action<IReadOnlyDictionary<string, string>>? NewEntry;

    internal JournalFollowSession(JournalOptions options, TimeSpan interval, Action onDispose, ILogger<SystemdJournal>? logger = null)
    {
        if (interval.TotalMilliseconds < 250)
            throw new InvalidOperationException("The interval must be greater then 250ms.");

        _journal = new(logger, options.OpeningMode);
        _onDispose = onDispose;
        _interval = interval;

        try
        {
            _journal.SeekTail();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error seeking journal tail.", ex);
        }
    }

    private async Task ProcessInterval(PeriodicTimer timer)
    {
        while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
        {
            try
            {
                while (_journal.Next())
                    NewEntry?.Invoke(_journal.GetCurrentEntry());
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Error processing journal.", ex);
            }
        }
    }

    public void Start(int logCount)
    {
        if (_timer is null)
        {
            _timer = new(_interval);

            GoBack(logCount);

            _worker = ProcessInterval(_timer);
        }
    }

    public async Task Stop()
    {
        if (_timer is not null)
        {
            _timer.Dispose();

            if (_worker is not null)
            {
                try
                {
                    await _worker.ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }

                _worker = null;
            }

            _timer = null;
        }
    }

    public void SetFilter(string filter)
    {
        if (_timer is not null)
            throw new InvalidOperationException("Cannot change filter while session is running.");

        try
        {
            Filter.Apply(filter, _journal);
            _journal.SeekTail();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error applying filters. ({filter})", ex);
        }
    }

    private void GoBack(int times)
    {
        for (var i = 0; i < times; i++)
            _journal.Previous();
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
        _journal.Dispose();
        _onDispose();
    }
}
