using ViciOne.Journal;

namespace Serilog.Sinks.Journal;

/// <summary>
/// Convenience helpers for configuring <see cref="JournalSpamGuardOptions"/> on the Serilog sink.
/// </summary>
public static class JournalSpamGuardOptionsExtensions
{
    public const string MessageTemplateField = "MESSAGE_TEMPLATE";

    public static JournalSpamGuardOptions AggregateByMessageTemplate(this JournalSpamGuardOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AggregationFields.Add(MessageTemplateField);

        return options;
    }
}
