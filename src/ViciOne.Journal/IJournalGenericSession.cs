namespace ViciOne.Journal;

public interface IJournalGenericSession : IDisposable
{
    string GetCursor();
    IEnumerable<IReadOnlyDictionary<string, string>> GetLogs(int size, bool forward = true, bool skipFirst = false);
    void SeekCursor(string cursor);
    void SeekHead();
    void SeekRealTime(DateTime timestamp);
    void SeekTail();
    void SetFilter(string filter);

    void SeekRealTime(DateTimeOffset timestamp)
        => SeekRealTime(timestamp.UtcDateTime);

    /// <summary>
    /// Reads only the given fields of the next entries.
    /// </summary>
    IEnumerable<JournalRecord> GetRecords(JournalFieldSet fields, int size, bool forward = true, bool skipFirst = false)
        => throw new NotSupportedException();
}
