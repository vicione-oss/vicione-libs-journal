namespace ViciOne.Journal;

public sealed class JournalSpamGuardOptions
{
    public bool Enabled { get; set; }

    public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromMilliseconds(250);

    public int SummaryCountThreshold { get; set; } = 100;

    public int MaxBucketCapacity { get; set; } = 512;

    public string SummaryMessageTemplate { get; set; } = "{message} (repeated {count} times in {window})";

    public string SummaryCountFieldName { get; set; } = "SPAM_COUNT";

    public string SummaryWindowFieldName { get; set; } = "SPAM_WINDOW_MS";

    public IList<string> AggregationFields { get; } = [];

    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
}
