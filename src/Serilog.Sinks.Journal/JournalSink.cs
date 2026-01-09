using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using ViciOne.Journal;

namespace Serilog.Sinks.Journal;

public sealed class JournalSink(IReadOnlyDictionary<string, object?>? metaDataFields, ITextFormatter formatter,
    bool writeCustomKeyValues, bool writeStructuredData) : ILogEventSink, IDisposable
{
    private readonly SystemdJournalWriter _journalWriter = new();
    private readonly ITextFormatter _formatter = formatter;
    private readonly IReadOnlyDictionary<string, object?> _metaDataFields = metaDataFields ?? new Dictionary<string, object?>();

    public JournalSink(IReadOnlyDictionary<string, object?>? metaDataFields, ITextFormatter formatter) : this(metaDataFields, formatter, false, true)
    {
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1848:LoggerMessage-Delegaten verwenden")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2254:Vorlage muss ein statischer Ausdruck sein")]
    public void Emit(LogEvent logEvent)
    {
        using var writer = new StringWriter();

        _formatter.Format(logEvent, writer);

        var text = writer.ToString();
        var fields = _metaDataFields.Concat(GetFieldsFromLogEvent(logEvent));
        var entry = JournalEntryFactory.CreateLogEntry(fields, text, ToLogLevel(logEvent.Level), writeCustomKeyValues,
            writeStructuredData);

        _journalWriter.WriteEntry(entry);
    }

    private static IEnumerable<KeyValuePair<string, object?>> GetFieldsFromLogEvent(LogEvent logEvent)
        => [.. logEvent.Properties.Select(p => new KeyValuePair<string, object?>(p.Key, RemoveQuotes(p.Value)))];

    private static object? RemoveQuotes(LogEventPropertyValue logEventValue)
    {
        var objectString = logEventValue.ToString();

        if (TryGetQuoteValue(objectString, out var valueWithoutQuotes))
            return valueWithoutQuotes;
        else
            return logEventValue;
    }

    private static bool TryGetQuoteValue(string str, out string value)
    {
        if (str.StartsWith('"') && str.EndsWith('"'))
        {
            value = str[1..(str.Length - 1)];

            return true;
        }
        else
        {
            value = str;

            return false;
        }
    }

    private static LogLevel ToLogLevel(LogEventLevel level)
        => level switch
        {
            LogEventLevel.Verbose => LogLevel.Trace,
            LogEventLevel.Debug => LogLevel.Debug,
            LogEventLevel.Information => LogLevel.Information,
            LogEventLevel.Warning => LogLevel.Warning,
            LogEventLevel.Error => LogLevel.Error,
            LogEventLevel.Fatal => LogLevel.Critical,
            _ => LogLevel.None,
        };

    public void Dispose()
        => _journalWriter.Dispose();
}
