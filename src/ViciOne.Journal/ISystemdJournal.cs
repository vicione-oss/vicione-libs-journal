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

    /// <returns>Whether the current entry is the one the cursor identifies.</returns>
    bool TestCursor(string cursor);
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
    /// Returns the times of the oldest and the newest entry of all journal files, ignoring matches.
    /// </summary>
    /// <returns><see langword="null"/> if the journal is empty.</returns>
    JournalTimeRange? GetTimeRange();

    /// <summary>
    /// Reads a field of the current entry without copying it.
    /// </summary>
    /// <param name="value">The value after <c>FIELD=</c>. It is only valid until the next call on this journal.</param>
    /// <returns><see langword="false"/> if the entry has no such field or it cannot be read.</returns>
    bool TryGetData(JournalField field, out ReadOnlySpan<byte> value);

    /// <summary>
    /// Starts <see cref="TryEnumerateData"/> over at the first field of the current entry.
    /// </summary>
    void RestartData();

    /// <summary>
    /// Reads the next field of the current entry without copying it. Cheaper than several
    /// <see cref="TryGetData"/> calls, each of which searches the entry's fields.
    /// </summary>
    /// <param name="data">The field in the form <c>FIELD=value</c>. It is only valid until the next call on this journal.</param>
    /// <returns><see langword="false"/> after the last field.</returns>
    bool TryEnumerateData(out ReadOnlySpan<byte> data);
}
