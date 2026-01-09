namespace ViciOne.Journal;

public sealed class JournalLookBehindSession : IDisposable
{
    private readonly IJournalGenericSession _genericSession;

    internal JournalLookBehindSession(IJournalGenericSession genericSession)
    {
        _genericSession = genericSession;
        _genericSession.SeekTail();
    }

    public void SetFilter(string filter)
    {
        _genericSession.SetFilter(filter);
        _genericSession.SeekTail();
    }

    public IEnumerable<IReadOnlyDictionary<string, string>> GetLogs(int size)
        => _genericSession.GetLogs(size, false);

    public void Dispose()
        => _genericSession.Dispose();
}
