namespace ViciOne.Journal;

internal sealed class SpamBucket
{
    private IReadOnlyDictionary<string, object?> _template;
    private long _firstTick;
    private long _lastTick;
    private int _suppressedCount;

    internal SpamBucket(IReadOnlyDictionary<string, object?> template, long createdTick)
    {
        _template = template;
        _firstTick = createdTick;
        _lastTick = createdTick;
        _suppressedCount = 0;
    }

    internal void Replace(IReadOnlyDictionary<string, object?> template, long nowTicks)
    {
        _template = template;
        _firstTick = nowTicks;
        _lastTick = nowTicks;
        _suppressedCount = 0;
    }

    internal int Record(long nowTicks)
    {
        _suppressedCount++;
        _lastTick = nowTicks;
        return _suppressedCount;
    }

    internal bool HasSuppressedEntries => _suppressedCount > 0;

    internal bool IsWindowExpired(long nowTicks, long windowTicks)
        => windowTicks > 0 && nowTicks - _lastTick >= windowTicks;

    internal bool ShouldFlush(long nowTicks, long windowTicks)
        => HasSuppressedEntries && IsWindowExpired(nowTicks, windowTicks);

    internal bool TryCreateSummary(long nowTicks, out SpamSummary summary)
    {
        if (!HasSuppressedEntries)
        {
            summary = default;
            return false;
        }

        var endTick = Math.Max(nowTicks, _lastTick);
        summary = new SpamSummary(_template, _suppressedCount, _firstTick, endTick);
        _suppressedCount = 0;
        return true;
    }
}
