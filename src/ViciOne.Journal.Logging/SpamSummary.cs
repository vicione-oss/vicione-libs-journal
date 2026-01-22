namespace ViciOne.Journal;

internal readonly struct SpamSummary
{
    internal SpamSummary(IReadOnlyDictionary<string, object?> template, int suppressedCount, long firstTick, long lastTick)
    {
        Template = template;
        SuppressedCount = suppressedCount;
        FirstTick = firstTick;
        LastTick = lastTick;
    }

    internal IReadOnlyDictionary<string, object?> Template { get; }
    internal int SuppressedCount { get; }
    private long FirstTick { get; }
    private long LastTick { get; }
    internal TimeSpan GetDuration(TimeProvider provider)
        => provider is null || LastTick <= FirstTick
            ? TimeSpan.Zero
            : provider.GetElapsedTime(FirstTick, LastTick);
}
