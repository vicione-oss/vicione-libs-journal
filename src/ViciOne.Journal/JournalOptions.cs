namespace ViciOne.Journal;

public class JournalOptions
{
    public JournalOpeningMode OpeningMode { get; set; } = JournalOpeningMode.LocalOnly;
    public bool EnableLogging { get; set; }

    /// <summary>
    /// A directory whose journal files to read instead of the system's journal, such as a copy taken from another device.
    /// <see cref="OpeningMode"/> does not apply to it.
    /// </summary>
    public string? Directory { get; set; }

    /// <summary>
    /// The number of generic and follow sessions that may be open at once. Each holds an open journal.
    /// </summary>
    public int MaxSessions { get; set; } = 5;

    /// <summary>
    /// The number of one-shot queries, such as <see cref="IJournalService.GetLogs"/>, that may run at once.
    /// </summary>
    public int MaxConcurrentQueries { get; set; } = 4;
}
