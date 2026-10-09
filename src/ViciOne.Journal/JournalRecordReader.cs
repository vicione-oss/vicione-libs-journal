using System.Text;

namespace ViciOne.Journal;

internal static class JournalRecordReader
{
    /// <remarks>
    /// Walks the entry's fields once: looking up each field separately searches all fields every time,
    /// which costs several times more for fields an entry does not have.
    /// </remarks>
    internal static JournalRecord Read(ISystemdJournal journal, JournalFieldSet fields)
    {
        var timestamp = JournalTime.FromRealtimeUsec(journal.GetRealtimeUsec());
        var cursor = journal.GetCursor();
        int? priority = null;
        var values = new string?[fields.Fields.Count];

        journal.RestartData();
        while (journal.TryEnumerateData(out var data))
        {
            var separator = data.IndexOf((byte)'=');
            if (separator <= 0)
                continue;

            var name = data[..separator];
            var value = data[(separator + 1)..];

            if (priority is null && JournalPriority.IsPriorityField(name) && JournalPriority.TryParse(value, out var parsed))
                priority = parsed;

            var index = fields.IndexOf(name);
            if (index >= 0 && values[index] is null)
                values[index] = Encoding.UTF8.GetString(value);
        }

        return new(timestamp, cursor, priority, fields, values);
    }
}
