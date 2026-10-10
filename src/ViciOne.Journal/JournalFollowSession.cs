using Microsoft.Extensions.Logging;

namespace ViciOne.Journal;

public sealed class JournalFollowSession : IJournalFollowSession
{
    private readonly JournalFollower<IReadOnlyDictionary<string, string>> _follower;

    internal JournalFollowSession(Func<ISystemdJournal> openJournal, TimeSpan interval, Action onDispose, ILogger<SystemdJournal>? logger = null)
        => _follower = new(openJournal, interval, j => j.GetCurrentEntry(), Deliver, ex => Error?.Invoke(ex), onDispose, logger);

    /// <summary>
    /// Raised when a new journal entry is received.
    /// </summary>
    public event Action<IReadOnlyDictionary<string, string>>? NewEntry;

    /// <summary>
    /// Raised when an error occurs during journal processing.
    /// The session will stop after raising this event.
    /// </summary>
    public event Action<Exception>? Error;

    public void Start(int logCount)
        => _follower.Start(logCount);

    public Task Stop()
        => _follower.Stop();

    public void SetFilter(string filter)
        => _follower.SetFilter(filter);

    public void Dispose()
        => _follower.Dispose();

    private void Deliver(List<IReadOnlyDictionary<string, string>> entries)
    {
        foreach (var entry in entries)
            NewEntry?.Invoke(entry);
    }
}
