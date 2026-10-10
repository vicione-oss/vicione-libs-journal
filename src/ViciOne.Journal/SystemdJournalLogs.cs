using Microsoft.Extensions.Logging;

namespace ViciOne.Journal;

internal static partial class SystemdJournalLogs
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Journal opened successfully with flags {Flags}")]
    public static partial void LogJournalOpened(this ILogger<SystemdJournal> logger, JournalOpeningMode flags);

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Journal disposed.")]
    public static partial void LogJournalDisposed(this ILogger<SystemdJournal> logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Reinitializing journal connection.")]
    public static partial void LogReinitializingJournal(this ILogger<SystemdJournal> logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Error disposing old journal handle during reinitialization.")]
    public static partial void LogErrorDisposingOldHandle(this ILogger<SystemdJournal> logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Failed to reinitialize journal connection. sd_journal_open returned error {Ret}")]
    public static partial void LogFailedReinitializeJournal(this ILogger<SystemdJournal> logger, int ret);

    [LoggerMessage(EventId = 5, Level = LogLevel.Debug, Message = "Journal connection reinitialized successfully.")]
    public static partial void LogJournalReinitialized(this ILogger<SystemdJournal> logger);

    [LoggerMessage(EventId = 6, Level = LogLevel.Debug, Message = "AddMatch called with match: {Match}")]
    public static partial void LogAddMatch(this ILogger<SystemdJournal> logger, string match);

    [LoggerMessage(EventId = 7, Level = LogLevel.Error, Message = "sd_journal_add_disjunction failed with error {Ret}")]
    public static partial void LogAddDisjunctionFailed(this ILogger<SystemdJournal> logger, int ret);

    [LoggerMessage(EventId = 8, Level = LogLevel.Debug, Message = "SeekHead called.")]
    public static partial void LogSeekHead(this ILogger<SystemdJournal> logger);

    [LoggerMessage(EventId = 9, Level = LogLevel.Debug, Message = "SeekTail called.")]
    public static partial void LogSeekTail(this ILogger<SystemdJournal> logger);

    [LoggerMessage(EventId = 10, Level = LogLevel.Debug, Message = "SeekRealtime called with time: {Time}")]
    public static partial void LogSeekRealtime(this ILogger<SystemdJournal> logger, DateTimeOffset time);

    [LoggerMessage(EventId = 11, Level = LogLevel.Debug, Message = "SeekCursor called with cursor: {Cursor}")]
    public static partial void LogSeekCursor(this ILogger<SystemdJournal> logger, string cursor);

    [LoggerMessage(EventId = 12, Level = LogLevel.Debug, Message = "Next() called.")]
    public static partial void LogNextCalled(this ILogger<SystemdJournal> logger);

    [LoggerMessage(EventId = 13, Level = LogLevel.Debug, Message = "GetCurrentEntry() called.")]
    public static partial void LogGetCurrentEntry(this ILogger<SystemdJournal> logger);

    [LoggerMessage(EventId = 14, Level = LogLevel.Error, Message = "sd_journal_enumerate_data failed with error {Ret}")]
    public static partial void LogEnumerateDataFailed(this ILogger<SystemdJournal> logger, int ret);

    [LoggerMessage(EventId = 15, Level = LogLevel.Debug, Message = "GetCursor() called.")]
    public static partial void LogGetCursor(this ILogger<SystemdJournal> logger);

    [LoggerMessage(EventId = 16, Level = LogLevel.Debug, Message = "GetRealtimeTimestamp() called.")]
    public static partial void LogGetRealtimeTimestamp(this ILogger<SystemdJournal> logger);

    [LoggerMessage(EventId = 17, Level = LogLevel.Debug, Message = "EnumerateAvailableFields() called.")]
    public static partial void LogEnumerateAvailableFields(this ILogger<SystemdJournal> logger);

    [LoggerMessage(EventId = 18, Level = LogLevel.Error, Message = "sd_journal_enumerate_fields failed with error {Ret}")]
    public static partial void LogEnumerateFieldsFailed(this ILogger<SystemdJournal> logger, int ret);

    [LoggerMessage(EventId = 19, Level = LogLevel.Debug, Message = "EnumerateUniqueFieldValues() called for field: {Field}")]
    public static partial void LogEnumerateUniqueFieldValues(this ILogger<SystemdJournal> logger, string field);

    [LoggerMessage(EventId = 20, Level = LogLevel.Error, Message = "sd_journal_query_unique failed for field {Field} with error {QueryResult}")]
    public static partial void LogQueryUniqueFailed(this ILogger<SystemdJournal> logger, string field, int queryResult);

    [LoggerMessage(EventId = 21, Level = LogLevel.Error, Message = "sd_journal_enumerate_unique failed with error {Ret}")]
    public static partial void LogEnumerateUniqueFailed(this ILogger<SystemdJournal> logger, int ret);

    [LoggerMessage(EventId = 24, Level = LogLevel.Error, Message = "{Function} failed with error {Ret}. Native error count: {Count}")]
    public static partial void LogNativeCallFailed(this ILogger<SystemdJournal> logger, string function, int ret, int count);

    [LoggerMessage(EventId = 25, Level = LogLevel.Warning, Message = "{Function} failed twice consecutively. Reinitializing journal connection.")]
    public static partial void LogReinitializeJournal(this ILogger<SystemdJournal> logger, string function);

    [LoggerMessage(EventId = 27, Level = LogLevel.Debug, Message = "Previous() called.")]
    public static partial void LogPreviousCalled(this ILogger<SystemdJournal> logger);
    [LoggerMessage(EventId = 28, Level = LogLevel.Warning, Message = "Attempted to read empty entry")]
    public static partial void LogReadEmptyEntry(this ILogger<SystemdJournal> logger);
}
