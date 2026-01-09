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
}
