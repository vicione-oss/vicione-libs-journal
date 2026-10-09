namespace ViciOne.Journal;

public class JournalOptions
{
    public JournalOpeningMode OpeningMode { get; set; } = JournalOpeningMode.LocalOnly;
    public bool EnableLogging { get; set; }

    /// <summary>
    /// The number of generic and follow sessions that may be open at once. Each holds an open journal.
    /// </summary>
    public int MaxSessions { get; set; } = 5;

    /// <summary>
    /// The number of one-shot queries, such as <see cref="IJournalService.GetLogs"/>, that may run at once.
    /// </summary>
    public int MaxConcurrentQueries { get; set; } = 4;
}
