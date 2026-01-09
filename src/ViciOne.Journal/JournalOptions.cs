namespace ViciOne.Journal;

public class JournalOptions
{
    public JournalOpeningMode OpeningMode { get; set; } = JournalOpeningMode.LocalOnly;
    public bool EnableLogging { get; set; }
}
