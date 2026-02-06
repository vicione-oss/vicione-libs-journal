namespace ViciOne.Journal;

public interface IJournalFollowSession : IDisposable
{
    event Action<Exception>? Error;
    event Action<IReadOnlyDictionary<string, string>>? NewEntry;

    void SetFilter(string filter);
    void Start(int logCount);
    Task Stop();
}
