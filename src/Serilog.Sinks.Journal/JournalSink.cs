using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using ViciOne.Journal;

namespace Serilog.Sinks.Journal;

public sealed class JournalSink : ILogEventSink, IDisposable
{
    private const string MessageTemplateFieldName = JournalSpamGuardOptionsExtensions.MessageTemplateField;

    private readonly SystemdJournalWriter _journalWriter = new();
    private readonly ITextFormatter _formatter;
    private readonly IReadOnlyDictionary<string, object?> _metaDataFields;
    private readonly JournalSpamAggregator? _spamAggregator;
    private readonly bool _writeCustomKeyValues;
    private readonly bool _writeStructuredData;

    public JournalSink(IReadOnlyDictionary<string, object?>? metaDataFields, ITextFormatter formatter)
        : this(metaDataFields, formatter, false, true)
    {
    }

    public JournalSink(IReadOnlyDictionary<string, object?>? metaDataFields, ITextFormatter formatter,
        bool writeCustomKeyValues, bool writeStructuredData, JournalSpamGuardOptions? spamGuardOptions = null)
    {
        _formatter = formatter;
        _metaDataFields = metaDataFields ?? new Dictionary<string, object?>();
        _writeCustomKeyValues = writeCustomKeyValues;
        _writeStructuredData = writeStructuredData;
        _spamAggregator = spamGuardOptions is { Enabled: true }
            ? new JournalSpamAggregator(spamGuardOptions, entry => _journalWriter.WriteEntry(entry))
            : null;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1848:LoggerMessage-Delegaten verwenden")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2254:Vorlage muss ein statischer Ausdruck sein")]
    public void Emit(LogEvent logEvent)
    {
        var entry = CreateEntry(logEvent, _formatter, _metaDataFields, _writeCustomKeyValues, _writeStructuredData);

        if (_spamAggregator is { } aggregator)
            aggregator.Enqueue(entry);
        else
            _journalWriter.WriteEntry(entry);
    }

    internal static Dictionary<string, object?> CreateEntry(LogEvent logEvent, ITextFormatter formatter,
        IReadOnlyDictionary<string, object?> metaDataFields, bool writeCustomKeyValues, bool writeStructuredData)
    {
        using var writer = new StringWriter();

        formatter.Format(logEvent, writer);

        var text = writer.ToString();
        var entry = JournalEntryFactory.CreateLogEntry(GetFieldsFromLogEvent(logEvent), text, ToLogLevel(logEvent.Level),
            writeCustomKeyValues, writeStructuredData, metaData: metaDataFields);
        entry[MessageTemplateFieldName] = logEvent.MessageTemplate.Text;
        return entry;
    }

    internal static IEnumerable<KeyValuePair<string, object?>> GetFieldsFromLogEvent(LogEvent logEvent)
    {
        // The raw message template is stable per call site (e.g. "Request {RequestId} failed"),
        // regardless of the rendered values. Surfacing it lets the spam guard aggregate floods
        // of unique messages that share a call site.
        foreach (var p in logEvent.Properties)
        {
            if (p.Key != MessageTemplateFieldName)
                yield return new KeyValuePair<string, object?>(p.Key, RemoveQuotes(p.Value));
        }

        yield return new KeyValuePair<string, object?>(MessageTemplateFieldName, logEvent.MessageTemplate.Text);
    }

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
    {
        _spamAggregator?.Dispose();
        _journalWriter.Dispose();
    }
}
