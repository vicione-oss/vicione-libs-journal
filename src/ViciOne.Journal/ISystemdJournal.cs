namespace ViciOne.Journal;

/// <summary>
/// The sd-journal operations the sessions and the service build on.
/// </summary>
/// <remarks>
/// An instance is bound to the thread that opened it, as sd-journal requires.
/// </remarks>
internal interface ISystemdJournal : IDisposable
{
    void AddMatch(string match);
    void AddDisjunction();
    void FlushMatches();
    void SeekHead();
    void SeekTail();
    void SeekRealtime(DateTimeOffset time);
    void SeekCursor(string cursor);
    bool Next();
    bool Previous();
    Dictionary<string, string> GetCurrentEntry();
    string GetCursor();
    List<string> EnumerateAvailableFields();
    List<string> EnumerateUniqueFieldValues(string field);
    Dictionary<int, int> GetPriorityCountsSince(DateTime since, CancellationToken cancellationToken = default);
    Dictionary<int, int> GetPriorityCountsBetween(DateTime start, DateTime end, CancellationToken cancellationToken = default);

    /// <summary>
    /// Picks up journal files that were created or rotated since the journal was opened.
    /// </summary>
    void ProcessChanges();
}
