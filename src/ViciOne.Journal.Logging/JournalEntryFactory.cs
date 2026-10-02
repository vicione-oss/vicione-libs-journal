using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace ViciOne.Journal;

internal partial class JournalEntryFactory
{
    private const string DefaultSdId = "journal@32473";
    private const int MaxJournalFieldLength = 32;
    private const string ReservedFieldPrefix = "PROP_";

    // Fields the sink writes itself or that journald and syslog consumers interpret (see systemd.journal-fields(7))
    // Custom key-values with these names are written with ReservedFieldPrefix instead
    private static readonly HashSet<string> s_journalReservedFields = new(StringComparer.Ordinal)
    {
        // Written by the sink
        "MESSAGE",
        "PRIORITY",
        "SYSLOG_STRUCTURED_DATA",

        // User journal fields with a special meaning
        "MESSAGE_ID",
        "CODE_FILE",
        "CODE_LINE",
        "CODE_FUNC",
        "ERRNO",
        "INVOCATION_ID",
        "USER_INVOCATION_ID",
        "SYSLOG_FACILITY",
        "SYSLOG_IDENTIFIER",
        "SYSLOG_PID",
        "SYSLOG_TIMESTAMP",
        "SYSLOG_RAW",
        "DOCUMENTATION",
        "TID",
        "UNIT",
        "USER_UNIT",
        "OBJECT_PID",
    };

    // RFC 5424 standard fields that should be excluded from STRUCTURED-DATA
    // These fields are part of the syslog message header
    private static readonly HashSet<string> s_rfc5424ExcludedFields = new(StringComparer.Ordinal)
    {
        // Core syslog fields
        "MESSAGE",           // MSG - The log message text
        "PRIORITY",          // PRI - Priority value

        // RFC 5424 HEADER fields
        "VERSION",           // Syslog protocol version
        "TIMESTAMP",         // Message timestamp
        "HOSTNAME",          // Hostname of the originator
        "SYSLOG_IDENTIFIER", // APP-NAME - Application identifier
        "SYSLOG_PID",        // PROCID - Process ID
        "SYSLOG_STRUCTURED_DATA",   // The structured data field itself
        "MESSAGE_ID",        // MSGID - Message type identifier

        // Common systemd journal fields that map to RFC 5424 header
        "SYSLOG_FACILITY",   // Syslog facility
        "REALTIME_TIMESTAMP",// Systemd realtime timestamp
        "MONOTONIC_TIMESTAMP", // Systemd monotonic timestamp

        // Process identification fields (part of PROCID)
        "PID",               // Process ID
        "_PID",              // Systemd process ID

        // System identification fields
        "_HOSTNAME",         // Systemd hostname
        "_MACHINE_ID",       // Machine identifier
        "_BOOT_ID",          // Boot identifier

        // Process context fields (typically header info)
        "_UID",              // User ID
        "_GID",              // Group ID
        "_COMM",             // Command name
        "_EXE",              // Executable path
        "_CMDLINE",          // Command line

        // Systemd unit fields
        "_SYSTEMD_UNIT",     // Systemd unit name
        "_SYSTEMD_USER_UNIT",// Systemd user unit
        "_SYSTEMD_SLICE",    // Systemd slice
        "_SYSTEMD_CGROUP",   // Systemd control group

        // Transport and source fields
        "_TRANSPORT",        // Transport mechanism
        "_SOURCE_REALTIME_TIMESTAMP", // Source timestamp
    };
    private static readonly ConcurrentDictionary<string, string> s_fieldNameCache = new(StringComparer.Ordinal);

    public static Dictionary<string, object?> CreateLogEntry(IEnumerable<KeyValuePair<string, object?>> keyValueList,
        string message, LogLevel logLevel, bool writeCustomKeyValues, bool writeStructuredData, string prefix = "")
    {
        Dictionary<string, object?> customFields = new(StringComparer.Ordinal);
        foreach (var (key, value) in keyValueList)
        {
            var fieldName = CreateFieldName(key);
            if (string.IsNullOrEmpty(fieldName))
                continue;
            customFields[fieldName] = value;
        }
        var structuredData = string.Empty;

        if (writeStructuredData)
        {
            // Add RFC 5424 compatible SYSLOG_STRUCTURED_DATA field
            structuredData = CreateStructuredData(customFields);
            if (string.IsNullOrEmpty(structuredData))
                writeStructuredData = false;
        }

        // MESSAGE and PRIORITY + customFields + SYSLOG_STRUCTURED_DATA
        var count = 2 + (writeCustomKeyValues ? customFields.Count : 0) + (writeStructuredData ? 1 : 0);
        Dictionary<string, object?> dictionary = new(count)
        {
            ["MESSAGE"] = $"{prefix}{message}",
            ["PRIORITY"] = MapLogLevelToJournalPriority(logLevel)
        };

        if (writeCustomKeyValues)
        {
            foreach (var (key, value) in customFields)
                dictionary[CreateCustomFieldName(key)] = value;
        }
        if (writeStructuredData)
            dictionary["SYSLOG_STRUCTURED_DATA"] = structuredData;

        return dictionary;
    }

    /// <summary>
    /// Creates an RFC 5424 compatible structured data string from the log entry fields.
    /// Format: [SD-ID param1="value1" param2="value2"]
    /// Excludes all RFC 5424 standard header fields from the structured data.
    /// </summary>
    /// <param name="fields">The fields to include in the structured data</param>
    /// <param name="sdId">The structured data ID (default: journal@32473)</param>
    /// <returns>RFC 5424 formatted structured data string or empty string if no custom fields</returns>
    internal static string CreateStructuredData(Dictionary<string, object?> fields, string sdId = DefaultSdId)
    {
        StringBuilder sb = new();
        sb.Append('[');
        sb.Append(sdId);

        var hasParams = false;
        foreach (var (key, value) in fields)
        {
            // Exclude RFC 5424 standard header fields
            if (s_rfc5424ExcludedFields.Contains(key))
                continue;

            var paramValue = value?.ToString() ?? string.Empty;

            sb.Append(' ');
            sb.Append(key);
            sb.Append("=\"");
            AppendEscapedStructuredDataValue(paramValue, sb);
            sb.Append('"');
            hasParams = true;
        }

        sb.Append(']');

        // If only the SD-ID was added (no parameters), return empty string to skip the field
        return hasParams ? sb.ToString() : string.Empty;
    }

    /// <summary>
    /// Escapes special characters in structured data parameter values according to RFC 5424.
    /// Escapes: '\' -> '\\', '"' -> '\"', ']' -> '\]'
    /// </summary>
    private static void AppendEscapedStructuredDataValue(string value, StringBuilder stringBuilder)
    {
        if (string.IsNullOrEmpty(value))
            return;

        foreach (var c in value)
        {
            if (c is '\\' or '"' or ']')
                stringBuilder.Append('\\');
            stringBuilder.Append(c);
        }
    }

    internal static string CreateFieldName(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName))
            return string.Empty;

        return s_fieldNameCache.GetOrAdd(keyName, static name =>
        {
            var nameWithUnderscores = InterpretCamelCase(name);
            StringBuilder nameBuilder = new(FieldNameRegex().Replace(nameWithUnderscores.ToUpperInvariant(), string.Empty));

            while (nameBuilder.Length > 0 && nameBuilder[0] == '_')
                nameBuilder.Remove(0, 1);

            if (nameBuilder.Length > MaxJournalFieldLength)
                nameBuilder.Length = MaxJournalFieldLength;

            return nameBuilder.ToString();
        });
    }

    // Keeps custom key-values from overwriting or changing the meaning of reserved journal fields
    internal static string CreateCustomFieldName(string fieldName)
        => s_journalReservedFields.Contains(fieldName) ? ReservedFieldPrefix + fieldName : fieldName;

    private static string InterpretCamelCase(string value)
        => CamelCaseRegex().Replace(value, "$1_$2");

    internal static string MapLogLevelToJournalPriority(LogLevel logLevel)
        => logLevel switch
        {
            LogLevel.Critical => "2",
            LogLevel.Error => "3",
            LogLevel.Warning => "4",
            LogLevel.Information => "6",
            LogLevel.Debug => "7",
            LogLevel.Trace => "7",
            _ => throw new ArgumentOutOfRangeException(nameof(logLevel), "Unsupported LogLevel."),
        };

    [GeneratedRegex(@"[^_A-Z0-9]")]
    private static partial Regex FieldNameRegex();

    [GeneratedRegex(@"([a-z])([A-Z])")]
    private static partial Regex CamelCaseRegex();
}
