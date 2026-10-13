namespace ViciOne.Journal;

/// <summary>
/// Journal entry counts per time bucket, priority and group.
/// </summary>
/// <remarks>
/// Bucket <c>i</c> holds the entries from <c>BucketEdges[i]</c> (inclusive) to <c>BucketEdges[i + 1]</c> (exclusive).
/// Entries without a <c>PRIORITY</c> between 0 and 7 are not counted. Entries are read in journal order: after the clock
/// was set back, entries of the range that follow more than 1,000 entries past its end are not counted.
/// </remarks>
public sealed class JournalHistogram
{
    /// <summary>
    /// The number of priority levels, from 0 (emergency) to 7 (debug).
    /// </summary>
    public const int PriorityLevels = 8;

    /// <summary>
    /// The field <see cref="IJournalService.GetPriorityHistogram(DateTimeOffset, DateTimeOffset, TimeSpan, string?, string?, CancellationToken)"/>
    /// groups by unless told otherwise.
    /// </summary>
    public const string DefaultGroupBy = "_SYSTEMD_UNIT";

    private readonly IReadOnlyList<int[]> _countsPerGroup;

    internal JournalHistogram(IReadOnlyList<DateTimeOffset> bucketEdges, IReadOnlyList<string> groups, IReadOnlyList<int[]> countsPerGroup)
    {
        BucketEdges = bucketEdges;
        Groups = groups;
        _countsPerGroup = countsPerGroup;
    }

    public IReadOnlyList<DateTimeOffset> BucketEdges { get; }

    public int BucketCount
        => BucketEdges.Count - 1;

    /// <summary>
    /// The values of the grouping field in the order they were first counted. Without grouping,
    /// and for entries that have neither the grouping field nor <c>SYSLOG_IDENTIFIER</c>, the group is empty.
    /// </summary>
    public IReadOnlyList<string> Groups { get; }

    public int GetCount(int bucket, int priority, int group)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bucket);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(bucket, BucketCount);
        ArgumentOutOfRangeException.ThrowIfNegative(priority);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(priority, PriorityLevels);

        return _countsPerGroup[group][(bucket * PriorityLevels) + priority];
    }

    /// <summary>
    /// Returns the count of all groups together.
    /// </summary>
    public int GetCount(int bucket, int priority)
    {
        var count = 0;
        for (var group = 0; group < Groups.Count; group++)
            count += GetCount(bucket, priority, group);
        return count;
    }
}
