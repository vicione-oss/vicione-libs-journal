namespace ViciOne.Journal;

/// <summary>
/// A journal entry reduced to its time, cursor, priority and the fields of a <see cref="JournalFieldSet"/>.
/// </summary>
public sealed class JournalRecord
{
    private readonly string?[] _values;

    internal JournalRecord(DateTimeOffset timestamp, string cursor, int? priority, JournalFieldSet fields, string?[] values)
    {
        Timestamp = timestamp;
        Cursor = cursor;
        Priority = priority;
        Fields = fields;
        _values = values;
    }

    /// <summary>
    /// When journald received the entry, in UTC.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Identifies the entry, for example to read the entries before or after it.
    /// </summary>
    public string Cursor { get; }

    /// <summary>
    /// The syslog priority from 0 (emergency) to 7 (debug), or <see langword="null"/> if the entry has none.
    /// </summary>
    public int? Priority { get; }

    public JournalFieldSet Fields { get; }

    /// <returns>The value, or <see langword="null"/> if the entry does not have the field.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is not part of <see cref="Fields"/>.</exception>
    public string? this[string field]
        => _values[Fields.IndexOf(field)];
}
