using Microsoft.Extensions.Logging;

namespace ViciOne.Journal;

public sealed class JournalRecordFollowSession : IJournalRecordFollowSession
{
    private readonly JournalFollower<JournalRecord> _follower;

    internal JournalRecordFollowSession(Func<ISystemdJournal> openJournal, TimeSpan interval, JournalFieldSet fields, Action onDispose, ILogger<SystemdJournal>? logger = null)
        => _follower = new(openJournal, interval, j => JournalRecordReader.Read(j, fields), records => NewRecords?.Invoke(records), ex => Error?.Invoke(ex), onDispose, logger);

    public event Action<IReadOnlyList<JournalRecord>>? NewRecords;

    public event Action<Exception>? Error;

    public void Start(int logCount)
        => _follower.Start(logCount);

    public Task Stop()
        => _follower.Stop();

    public void SetFilter(string filter)
        => _follower.SetFilter(filter);

    public void Dispose()
        => _follower.Dispose();
}
