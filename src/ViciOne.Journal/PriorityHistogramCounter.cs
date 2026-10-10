using System.Text;

namespace ViciOne.Journal;

/// <summary>
/// Counts entries per bucket, priority and group in one pass, without allocating per entry.
/// </summary>
internal static class PriorityHistogramCounter
{
    internal const int MaxBuckets = 100_000;

    // 16 MB of counters. The group count is only known while counting, so the limit applies when a group is added.
    internal const int MaxCounters = 4_000_000;

    // After the clock was set back, entries of the range can follow later ones. Stopping at the first entry past the end
    // would miss them, and never stopping would read the rest of the journal for every range in the past.
    internal const int EntriesPastEndBeforeStop = 1_000;

    private static readonly JournalField s_priority = new("PRIORITY");
    private static readonly JournalField s_fallbackGroup = new("SYSLOG_IDENTIFIER");

    internal static IReadOnlyList<DateTimeOffset> CreateBucketEdges(DateTimeOffset start, DateTimeOffset end, TimeSpan bucketSize)
    {
        if (start >= end)
            throw new ArgumentException("Start must be earlier than end.", nameof(start));
        if (bucketSize <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(bucketSize), bucketSize, "Bucket size must be positive.");

        var bucketCount = Math.Ceiling((end - start) / bucketSize);
        if (bucketCount > MaxBuckets)
            throw new ArgumentOutOfRangeException(nameof(bucketSize), bucketSize, $"The range would need more than {MaxBuckets} buckets.");

        var edges = new List<DateTimeOffset>((int)bucketCount + 1);
        for (var i = 0; i < bucketCount; i++)
            edges.Add(start + (bucketSize * i));
        edges.Add(end);
        return edges;
    }

    internal static void Validate(IReadOnlyList<DateTimeOffset> bucketEdges, string? groupBy)
    {
        if (groupBy is not null && !SystemdJournal.FieldRegex().IsMatch(groupBy))
            throw new ArgumentException($"Invalid field format: {groupBy}. Only uppercase letters, digits, and underscores are allowed.", nameof(groupBy));
        if (bucketEdges.Count < 2)
            throw new ArgumentException("At least two bucket edges are required.", nameof(bucketEdges));
        if (bucketEdges.Count > MaxBuckets + 1)
            throw new ArgumentException($"At most {MaxBuckets} buckets are allowed.", nameof(bucketEdges));
        if (bucketEdges[0] < DateTimeOffset.UnixEpoch)
            throw new ArgumentOutOfRangeException(nameof(bucketEdges), bucketEdges[0], "The journal has no entries before 1970.");

        for (var i = 1; i < bucketEdges.Count; i++)
        {
            if (bucketEdges[i] <= bucketEdges[i - 1])
                throw new ArgumentException("Bucket edges must be strictly increasing.", nameof(bucketEdges));
        }
    }

    /// <param name="groupBy">The field to group by, or <see langword="null"/> to count all entries in one group.</param>
    internal static JournalHistogram Count(ISystemdJournal journal, IReadOnlyList<DateTimeOffset> bucketEdges, string? groupBy, CancellationToken cancellationToken)
    {
        var edges = bucketEdges.Select(JournalTime.ToRealtimeUsec).ToArray();
        var groups = new GroupTable(groupBy is null ? null : new JournalField(groupBy), bucketCount: edges.Length - 1);

        journal.SeekRealtime(bucketEdges[0]);
        var entriesPastEnd = 0;

        while (journal.Next())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var usec = journal.GetRealtimeUsec();
            if (usec >= edges[^1])
            {
                if (++entriesPastEnd == EntriesPastEndBeforeStop)
                    break;
                continue;
            }

            entriesPastEnd = 0;

            var bucket = FindBucket(edges, usec);
            if (bucket < 0 || !TryReadPriority(journal, out var priority))
                continue;

            groups.GetCountsOfCurrentEntry(journal)[(bucket * JournalHistogram.PriorityLevels) + priority]++;
        }

        return new(bucketEdges, groups.Names, groups.Counts);
    }

    // A clock change can put an entry before the first edge even after seeking to it.
    private static int FindBucket(ulong[] edges, ulong usec)
    {
        var index = Array.BinarySearch(edges, usec);
        return index >= 0 ? index : ~index - 1;
    }

    private static bool TryReadPriority(ISystemdJournal journal, out int priority)
    {
        priority = -1;
        if (!journal.TryGetData(s_priority, out var value) || value.Length != 1)
            return false;

        priority = value[0] - '0';
        return priority is >= 0 and < JournalHistogram.PriorityLevels;
    }

    private sealed class GroupTable
    {
        private readonly JournalField? _groupBy;
        private readonly int _countsPerGroup;
        private readonly Dictionary<byte[], int>.AlternateLookup<ReadOnlySpan<byte>> _indices
            = new Dictionary<byte[], int>(ByteSequenceComparer.Instance).GetAlternateLookup<ReadOnlySpan<byte>>();

        public GroupTable(JournalField? groupBy, int bucketCount)
        {
            _groupBy = groupBy;
            _countsPerGroup = bucketCount * JournalHistogram.PriorityLevels;
        }

        public List<string> Names { get; } = [];

        public List<int[]> Counts { get; } = [];

        public int[] GetCountsOfCurrentEntry(ISystemdJournal journal)
        {
            var value = ReadGroupValue(journal);
            if (!_indices.TryGetValue(value, out var index))
            {
                if ((long)(Counts.Count + 1) * _countsPerGroup > MaxCounters)
                    throw new InvalidOperationException($"More than {Counts.Count} groups of {_countsPerGroup / JournalHistogram.PriorityLevels} buckets exceed the counter limit. Use fewer buckets, a filter or no grouping.");

                index = Names.Count;
                Names.Add(Encoding.UTF8.GetString(value));
                Counts.Add(new int[_countsPerGroup]);
                _indices[value] = index;
            }

            return Counts[index];
        }

        private ReadOnlySpan<byte> ReadGroupValue(ISystemdJournal journal)
        {
            if (_groupBy is null)
                return [];
            if (journal.TryGetData(_groupBy, out var value) || journal.TryGetData(s_fallbackGroup, out value))
                return value;
            return [];
        }
    }

    private sealed class ByteSequenceComparer : IEqualityComparer<byte[]>, IAlternateEqualityComparer<ReadOnlySpan<byte>, byte[]>
    {
        public static ByteSequenceComparer Instance { get; } = new();

        public bool Equals(byte[]? x, byte[]? y)
            => x.AsSpan().SequenceEqual(y);

        public int GetHashCode(byte[] obj)
            => GetHashCode(obj.AsSpan());

        public bool Equals(ReadOnlySpan<byte> alternate, byte[] other)
            => alternate.SequenceEqual(other);

        public int GetHashCode(ReadOnlySpan<byte> alternate)
        {
            HashCode hash = new();
            hash.AddBytes(alternate);
            return hash.ToHashCode();
        }

        public byte[] Create(ReadOnlySpan<byte> alternate)
            => alternate.ToArray();
    }
}
