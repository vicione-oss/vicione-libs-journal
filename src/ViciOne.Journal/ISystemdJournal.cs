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

    /// <summary>
    /// Picks up journal files that were created or rotated since the journal was opened.
    /// </summary>
    void ProcessChanges();

    ulong GetRealtimeUsec();

    /// <summary>
    /// Reads a field of the current entry without copying it.
    /// </summary>
    /// <param name="value">The value after <c>FIELD=</c>. It is only valid until the next call on this journal.</param>
    /// <returns><see langword="false"/> if the entry has no such field or it cannot be read.</returns>
    bool TryGetData(JournalField field, out ReadOnlySpan<byte> value);
}
