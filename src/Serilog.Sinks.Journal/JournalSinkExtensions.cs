using Serilog.Configuration;
using Serilog.Formatting.Display;
using ViciOne.Journal;

namespace Serilog.Sinks.Journal;

public static class JournalSinkExtensions
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Objekte verwerfen, bevor Bereich verloren geht", Justification = "Serlilog ruft dispose in Log.CloseAndFlush")]
    public static LoggerConfiguration Journal(
        this LoggerSinkConfiguration configuration,
        IFormatProvider? formatProvider = null,
        string outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        IReadOnlyDictionary<string, object?>? metaDataFields = null,
        bool writeCustomKeyValues = false,
        bool writeStructuredData = true,
        Action<JournalSpamGuardOptions>? configureSpamGuard = null)
    {
        var formatter = new MessageTemplateTextFormatter(outputTemplate, formatProvider);

        JournalSpamGuardOptions? spamGuardOptions = null;

        if (configureSpamGuard is not null)
        {
            spamGuardOptions = new JournalSpamGuardOptions { Enabled = true };
            configureSpamGuard(spamGuardOptions);
        }

        return configuration.Sink(new JournalSink(metaDataFields, formatter, writeCustomKeyValues, writeStructuredData, spamGuardOptions));
    }
}
