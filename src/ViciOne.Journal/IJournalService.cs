
namespace ViciOne.Journal;

public interface IJournalService : IDisposable
{
    Task<IJournalFollowSession> CreateFollower(TimeSpan interval, CancellationToken cancellation = default);

    /// <summary>
    /// Follows new entries, reading only the given fields and delivering one batch per <paramref name="interval"/>.
    /// </summary>
    Task<IJournalRecordFollowSession> CreateFollower(TimeSpan interval, JournalFieldSet fields, CancellationToken cancellation = default)
        => throw new NotSupportedException();

    Task<IJournalGenericSession> CreateGeneric(CancellationToken cancellationToken = default);
    IReadOnlyDictionary<string, string> GetFieldDescriptions();
    Task<IReadOnlyCollection<string>> GetFields(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<string>> GetFieldValues(string field, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<IReadOnlyDictionary<string, string>>> GetLogs(DateTimeOffset since, string order = "desc", int limit = 100, string? filter = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, int>> GetPriorityCounts(DateTimeOffset since, string? filter = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, int>> GetPriorityCounts(DateTimeOffset start, DateTimeOffset end, string? filter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the entries per bucket, priority and group in one pass over the range.
    /// </summary>
    /// <param name="bucketSize">The width of each bucket; the last bucket ends at <paramref name="end"/> and may be shorter.</param>
    /// <param name="groupBy">
    /// The field to group by, falling back to <c>SYSLOG_IDENTIFIER</c> for entries without it,
    /// or <see langword="null"/> to count all entries in one group.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when reading fails, or when the buckets times the groups found need more than 4 million counters.
    /// </exception>
    Task<JournalHistogram> GetPriorityHistogram(DateTimeOffset start, DateTimeOffset end, TimeSpan bucketSize, string? filter = null, string? groupBy = JournalHistogram.DefaultGroupBy, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <summary>
    /// Counts the entries per bucket, priority and group in one pass over the range.
    /// </summary>
    /// <param name="bucketEdges">
    /// The strictly increasing bucket boundaries. Pass explicit edges for calendar buckets in local time,
    /// such as days across a daylight saving change.
    /// </param>
    /// <param name="groupBy">
    /// The field to group by, falling back to <c>SYSLOG_IDENTIFIER</c> for entries without it,
    /// or <see langword="null"/> to count all entries in one group.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when reading fails, or when the buckets times the groups found need more than 4 million counters.
    /// </exception>
    Task<JournalHistogram> GetPriorityHistogram(IReadOnlyList<DateTimeOffset> bucketEdges, string? filter = null, string? groupBy = JournalHistogram.DefaultGroupBy, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
