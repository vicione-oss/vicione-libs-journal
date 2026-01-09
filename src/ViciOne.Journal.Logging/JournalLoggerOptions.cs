using System.Diagnostics;

namespace ViciOne.Journal;

public sealed class JournalLoggerOptions
{
    public JournalOpeningMode OpenMode { get; set; } = JournalOpeningMode.LocalOnly;
    public Action<string> OnError { get; set; } = message => Debug.Fail("Logging to Journal failed.", message);
    public string EntryPrefix { get; set; } = string.Empty;
    public Dictionary<string, object?> MetaDataFields { get; } = [];
    public bool WriteStructuredData { get; set; } = true;
    public bool WriteCustomKeyValues { get; set; }
    public JournalSpamGuardOptions SpamGuard { get; } = new();
}
