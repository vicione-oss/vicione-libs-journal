namespace ViciOne.Journal;

/// <summary>
/// Follows new journal entries like <see cref="IJournalFollowSession"/>, but reads only the fields of a
/// <see cref="JournalFieldSet"/> and delivers the entries of each interval as one batch.
/// </summary>
public interface IJournalRecordFollowSession : IDisposable
{
    /// <summary>
    /// Raised once per interval with the new entries, oldest first. Not raised when there are none.
    /// </summary>
    event Action<IReadOnlyList<JournalRecord>>? NewRecords;

    /// <summary>
    /// Raised when reading fails. The session stops after raising it.
    /// </summary>
    event Action<Exception>? Error;

    /// <summary>
    /// Applies a filter and moves to the end of the journal. Only allowed before <see cref="Start"/>.
    /// </summary>
    void SetFilter(string filter);

    /// <param name="logCount">The number of recent entries the first batch includes.</param>
    void Start(int logCount);

    Task Stop();
}
