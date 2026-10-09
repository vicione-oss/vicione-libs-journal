namespace ViciOne.Journal;

/// <summary>
/// Converts between instants and the journal's realtime timestamps, in microseconds since the Unix epoch.
/// </summary>
internal static class JournalTime
{
    public static ulong ToRealtimeUsec(DateTimeOffset time)
    {
        if (time < DateTimeOffset.UnixEpoch)
            throw new ArgumentOutOfRangeException(nameof(time), time, "The journal has no entries before 1970.");

        return (ulong)(time - DateTimeOffset.UnixEpoch).Ticks / 10;
    }
}
