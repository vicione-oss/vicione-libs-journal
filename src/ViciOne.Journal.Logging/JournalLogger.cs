using Microsoft.Extensions.Logging;

namespace ViciOne.Journal;

internal partial class JournalLogger(string prefix, string category, IExternalScopeProvider? externalScopeProvider,
    Action<IReadOnlyDictionary<string, object?>> log, IReadOnlyDictionary<string, object?> metaData,
    bool writeCustomKeyValues, bool writeStructuredData) : ILogger
{
    private const string TemplateKey = "{OriginalFormat}";

    private readonly IExternalScopeProvider? _externalScopeProvider = externalScopeProvider;
    private readonly Action<IReadOnlyDictionary<string, object?>> _log = log;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _externalScopeProvider?.Push(state);

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel) && state is IReadOnlyList<KeyValuePair<string, object?>> keyValueList)
        {
            var list = keyValueList.Where(l => l.Key != TemplateKey);

            IEnumerable<KeyValuePair<string, object?>> metaDataFields = metaData;
            if (!string.IsNullOrEmpty(category))
                metaDataFields = metaDataFields.Append(new KeyValuePair<string, object?>("CATEGORY", category));

            if (eventId.Id != 0 || !string.IsNullOrEmpty(eventId.Name))
                metaDataFields = metaDataFields.Append(new KeyValuePair<string, object?>("EVENT_ID", FormatEventId(eventId)));

            var entry = JournalEntryFactory.CreateLogEntry(list, formatter(state, exception), logLevel,
                writeCustomKeyValues, writeStructuredData, prefix, metaDataFields);

            if (!string.IsNullOrEmpty(category))
                entry["CATEGORY"] = category;

            if (eventId.Id != 0 || !string.IsNullOrEmpty(eventId.Name))
                entry["EVENT_ID"] = FormatEventId(eventId);

            _log(entry);
        }
    }

    private static object FormatEventId(EventId eventId)
        => string.IsNullOrEmpty(eventId.Name)
            ? eventId.Id
            : $"{eventId.Id}:{eventId.Name}";
}
