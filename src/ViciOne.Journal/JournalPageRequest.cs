namespace ViciOne.Journal;

public enum JournalDirection
{
    Older,
    Newer,
}

/// <summary>
/// Which entries <see cref="IJournalService.ReadPage"/> reads.
/// </summary>
/// <remarks>
/// Without <see cref="Cursor"/> and <see cref="Time"/>, <see cref="JournalDirection.Older"/> reads the newest entries
/// and <see cref="JournalDirection.Newer"/> the oldest.
/// </remarks>
public sealed record JournalPageRequest(JournalFieldSet Fields)
{
    public int Count { get; init; } = 100;

    public JournalDirection Direction { get; init; } = JournalDirection.Older;

    /// <summary>
    /// Reads the entries before or after this entry, without the entry itself. If the entry is gone,
    /// for example removed by vacuuming, the page starts with the entry next to where it was.
    /// </summary>
    public string? Cursor { get; init; }

    /// <summary>
    /// Reads the entries at or after this instant with <see cref="JournalDirection.Newer"/>,
    /// and the entries before it with <see cref="JournalDirection.Older"/>.
    /// </summary>
    public DateTimeOffset? Time { get; init; }

    /// <summary>
    /// A filter in the syntax of <see cref="IJournalService.GetLogs"/>.
    /// </summary>
    public string? Filter { get; init; }
}

/// <param name="Records">The entries of the page, oldest first in either direction.</param>
/// <param name="HasMore">Whether more entries follow in the direction that was read.</param>
public sealed record JournalPage(IReadOnlyList<JournalRecord> Records, bool HasMore);
