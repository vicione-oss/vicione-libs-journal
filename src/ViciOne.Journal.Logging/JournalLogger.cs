using Microsoft.Extensions.Logging;

namespace ViciOne.Journal;

internal partial class JournalLogger(string prefix, string category, IExternalScopeProvider? externalScopeProvider,
    Action<IReadOnlyDictionary<string, object?>> log, IReadOnlyDictionary<string, object?> metaData,
    bool writeCustomKeyValues, bool writeStructuredData, IReadOnlySet<string>? spamGuardAggregationFields = null) : ILogger
{
    private const string TemplateKey = "{OriginalFormat}";

    // CATEGORY and EVENT_ID are reserved: state/scope properties that happen to share these
    // names (e.g. a log template placeholder named "EventId") must not impersonate them.
    private static readonly HashSet<string> s_reservedFieldNames = new(StringComparer.Ordinal) { "CATEGORY", "EVENT_ID" };
    private static readonly IReadOnlySet<string> s_noAggregationFields = new HashSet<string>();

    private readonly IExternalScopeProvider? _externalScopeProvider = externalScopeProvider;
    private readonly Action<IReadOnlyDictionary<string, object?>> _log = log;
    private readonly IReadOnlySet<string> _spamGuardAggregationFields = spamGuardAggregationFields ?? s_noAggregationFields;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _externalScopeProvider?.Push(state);

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel) && state is IReadOnlyList<KeyValuePair<string, object?>> keyValueList)
        {
            var list = keyValueList.Where(l => l.Key != TemplateKey && !s_reservedFieldNames.Contains(JournalEntryFactory.CreateFieldName(l.Key)));

            var hasEventId = eventId.Id != 0 || !string.IsNullOrEmpty(eventId.Name);

            IEnumerable<KeyValuePair<string, object?>> metaDataFields = metaData;

            // CATEGORY's behavior is unchanged and pre-dates this branch: it always feeds
            // customFields, and therefore both the top-level output (when WriteCustomKeyValues
            // is true) and SYSLOG_STRUCTURED_DATA (when WriteStructuredData is true),
            // regardless of WriteCustomKeyValues.
            if (!string.IsNullOrEmpty(category))
                metaDataFields = metaDataFields.Append(new KeyValuePair<string, object?>("CATEGORY", category));

            // EVENT_ID is new: it only feeds customFields - and therefore both the top-level
            // output and SYSLOG_STRUCTURED_DATA - when the operator explicitly opted into
            // custom key-values. Feeding it in unconditionally here would leak it into
            // SYSLOG_STRUCTURED_DATA regardless of WriteCustomKeyValues, since CreateLogEntry
            // builds structured data from every field it's handed, not just the ones it also
            // writes to the top level.
            if (hasEventId && writeCustomKeyValues)
                metaDataFields = metaDataFields.Append(new KeyValuePair<string, object?>("EVENT_ID", FormatEventId(eventId)));

            var entry = JournalEntryFactory.CreateLogEntry(list, formatter(state, exception), logLevel,
                writeCustomKeyValues, writeStructuredData, prefix, metaDataFields);

            // When custom key-values are disabled, the spam guard can still key on EVENT_ID,
            // but only when it's actually part of the resolved aggregation fields, and only as
            // a bare top-level field (bypassing SYSLOG_STRUCTURED_DATA entirely). Consumers
            // with spam guard disabled, or aggregating on other fields, see no new fields on
            // their entries at all.
            if (hasEventId && !writeCustomKeyValues && _spamGuardAggregationFields.Contains("EVENT_ID"))
                entry["EVENT_ID"] = FormatEventId(eventId);

            _log(entry);
        }
    }

    private static object FormatEventId(EventId eventId)
        => string.IsNullOrEmpty(eventId.Name)
            ? eventId.Id
            : $"{eventId.Id}:{eventId.Name}";
}
