namespace ViciOne.Journal;

internal static class JournalPageReader
{
    internal const int MaxCount = 10_000;

    internal static void Validate(JournalPageRequest request)
    {
        if (request.Count is < 1 or > MaxCount)
            throw new ArgumentOutOfRangeException(nameof(request), request.Count, $"Count must be between 1 and {MaxCount}.");
        if (request.Cursor is not null && request.Time is not null)
            throw new ArgumentException("Set either a cursor or a time, not both.", nameof(request));
        if (request.Time < DateTimeOffset.UnixEpoch)
            throw new ArgumentOutOfRangeException(nameof(request), request.Time, "The journal has no entries before 1970.");
    }

    internal static JournalPage Read(ISystemdJournal journal, JournalPageRequest request, CancellationToken cancellationToken)
    {
        Func<bool> step = request.Direction == JournalDirection.Newer ? journal.Next : journal.Previous;
        var records = new List<JournalRecord>(request.Count);

        if (MoveToFirstEntry(journal, request, step))
        {
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                records.Add(JournalRecordReader.Read(journal, request.Fields));
            }
            while (records.Count < request.Count && step());
        }

        var hasMore = records.Count == request.Count && step();
        if (request.Direction == JournalDirection.Older)
            records.Reverse();

        return new(records, hasMore);
    }

    /// <returns><see langword="false"/> if the page is empty.</returns>
    private static bool MoveToFirstEntry(ISystemdJournal journal, JournalPageRequest request, Func<bool> step)
    {
        if (request.Cursor is { } cursor)
        {
            journal.SeekCursor(cursor);

            // The step lands on the cursor's entry, which the page excludes, or on its neighbour if the entry is gone.
            return step() && (!journal.TestCursor(cursor) || step());
        }

        if (request.Time is { } time)
        {
            journal.SeekRealtime(time);
            return request.Direction == JournalDirection.Newer ? step() : StepBefore(journal, time, step);
        }

        if (request.Direction == JournalDirection.Newer)
            journal.SeekHead();
        else
            journal.SeekTail();

        return step();
    }

    // Stepping back after a seek lands on the last entry at or before the time, but the page excludes the time itself.
    private static bool StepBefore(ISystemdJournal journal, DateTimeOffset time, Func<bool> step)
    {
        var boundary = JournalTime.ToRealtimeUsec(time);
        while (step())
        {
            if (journal.GetRealtimeUsec() < boundary)
                return true;
        }

        return false;
    }
}
