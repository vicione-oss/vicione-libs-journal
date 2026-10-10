using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using ViciOne.Journal;

namespace Journal.Tests;

internal sealed record FakeJournalEntry(long Sequence, DateTimeOffset Timestamp, IReadOnlyDictionary<string, string> Fields)
{
    private static long s_sequence;

    public string Cursor
        => $"s=fake;i={Sequence}";

    /// <param name="fields">Assignments in the form <c>FIELD=value</c>.</param>
    public static FakeJournalEntry At(DateTimeOffset timestamp, params string[] fields)
        => new(Interlocked.Increment(ref s_sequence), timestamp, fields.Select(f => f.Split('=', 2)).ToDictionary(f => f[0], f => f[1]));
}

/// <summary>
/// An in-memory journal over a shared, time-ordered entry list that follows the sd-journal
/// rules for seeking, stepping and matching. Entries appended to the list after a seek
/// become visible to later <see cref="Next"/> calls, as new entries do in a real journal.
/// </summary>
/// <param name="newFileEntries">
/// Entries in a journal file created after opening, such as after a rotation. Like sd-journal,
/// the fake only sees them after <see cref="ProcessChanges"/>.
/// </param>
internal sealed class FakeSystemdJournal(List<FakeJournalEntry> entries, ConcurrentQueue<FakeJournalEntry>? newFileEntries = null) : ISystemdJournal
{
    private readonly List<List<string>> _matchGroups = [[]];
    private int? _current;
    private int _nextCandidate;
    private int _previousCandidate = -1;

    public bool IsDisposed { get; private set; }

    public void Dispose()
        => IsDisposed = true;

    public void AddMatch(string match)
        => _matchGroups[^1].Add(match);

    public void AddDisjunction()
    {
        if (_matchGroups[^1].Count > 0)
            _matchGroups.Add([]);
    }

    public void FlushMatches()
    {
        _matchGroups.Clear();
        _matchGroups.Add([]);
    }

    public void SeekHead()
        => Seek(0, -1);

    public void SeekTail()
        => Seek(entries.Count, entries.Count - 1);

    public void SeekRealtime(DateTimeOffset time)
    {
        _ = JournalTime.ToRealtimeUsec(time);
        var firstAtOrAfter = entries.FindIndex(e => e.Timestamp >= time);

        Seek(firstAtOrAfter < 0 ? entries.Count : firstAtOrAfter, entries.FindLastIndex(e => e.Timestamp <= time));
    }

    public void SeekCursor(string cursor)
    {
        var sequence = long.Parse(cursor[(cursor.LastIndexOf('=') + 1)..], CultureInfo.InvariantCulture);
        var firstAtOrAfter = entries.FindIndex(e => e.Sequence >= sequence);

        Seek(firstAtOrAfter < 0 ? entries.Count : firstAtOrAfter, entries.FindLastIndex(e => e.Sequence <= sequence));
    }

    public bool Next()
    {
        for (var i = _nextCandidate; i < entries.Count; i++)
        {
            if (Matches(entries[i]))
                return MoveTo(i);
        }

        return false;
    }

    public bool Previous()
    {
        for (var i = Math.Min(_previousCandidate, entries.Count - 1); i >= 0; i--)
        {
            if (Matches(entries[i]))
                return MoveTo(i);
        }

        return false;
    }

    public Dictionary<string, string> GetCurrentEntry()
    {
        var entry = CurrentEntry;

        return new(entry.Fields)
        {
            ["REALTIME_TIMESTAMP"] = entry.Timestamp.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
        };
    }

    public string GetCursor()
        => CurrentEntry.Cursor;

    public List<string> EnumerateAvailableFields()
        => [.. entries.SelectMany(e => e.Fields.Keys).Distinct()];

    public List<string> EnumerateUniqueFieldValues(string field)
        => [.. entries.Where(e => e.Fields.ContainsKey(field)).Select(e => $"{field}={e.Fields[field]}").Distinct()];

    public void ProcessChanges()
    {
        while (newFileEntries is not null && newFileEntries.TryDequeue(out var entry))
            entries.Add(entry);
    }

    public ulong GetRealtimeUsec()
        => (ulong)(CurrentEntry.Timestamp - DateTimeOffset.UnixEpoch).Ticks / 10;

    public bool TryGetData(JournalField field, out ReadOnlySpan<byte> value)
    {
        value = CurrentEntry.Fields.TryGetValue(field.Name, out var text) ? Encoding.UTF8.GetBytes(text) : default;
        return text is not null;
    }

    public Dictionary<int, int> GetPriorityCountsSince(DateTime since, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Dictionary<int, int> GetPriorityCountsBetween(DateTime start, DateTime end, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    private FakeJournalEntry CurrentEntry
        => entries[_current ?? throw new InvalidOperationException("No current entry.")];

    private void Seek(int nextCandidate, int previousCandidate)
    {
        _current = null;
        _nextCandidate = nextCandidate;
        _previousCandidate = previousCandidate;
    }

    private bool MoveTo(int index)
    {
        _current = index;
        _nextCandidate = index + 1;
        _previousCandidate = index - 1;
        return true;
    }

    private bool Matches(FakeJournalEntry entry)
        => _matchGroups.All(g => g.Count == 0) || _matchGroups.Any(g => g.Count > 0 && MatchesGroup(entry, g));

    // Within a group, matches on the same field are alternatives and different fields must all match.
    private static bool MatchesGroup(FakeJournalEntry entry, List<string> group)
        => group
            .Select(m => m.Split('=', 2))
            .GroupBy(m => m[0], m => m[1])
            .All(field => entry.Fields.TryGetValue(field.Key, out var value) && field.Contains(value));
}
