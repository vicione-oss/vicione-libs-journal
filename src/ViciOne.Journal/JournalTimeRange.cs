namespace ViciOne.Journal;

/// <param name="Oldest">The time of the oldest entry still in the journal.</param>
/// <param name="Newest">The time of the newest entry.</param>
public sealed record JournalTimeRange(DateTimeOffset Oldest, DateTimeOffset Newest);
