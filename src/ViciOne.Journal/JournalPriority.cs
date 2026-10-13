namespace ViciOne.Journal;

internal static class JournalPriority
{
    private static readonly JournalField s_field = new("PRIORITY");

    /// <returns><see langword="false"/> if the current entry has no priority from 0 to 7.</returns>
    internal static bool TryRead(ISystemdJournal journal, out int priority)
    {
        priority = -1;
        return journal.TryGetData(s_field, out var value) && TryParse(value, out priority);
    }

    internal static bool IsPriorityField(ReadOnlySpan<byte> name)
        => name.SequenceEqual(s_field.NativeName.AsSpan(..^1));

    internal static bool TryParse(ReadOnlySpan<byte> value, out int priority)
    {
        priority = value.Length == 1 ? value[0] - '0' : -1;
        return priority is >= 0 and < JournalHistogram.PriorityLevels;
    }
}
