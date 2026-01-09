namespace ViciOne.Journal;

/// <summary>
/// Provides a list of available fields in the journal.
/// </summary>
internal static class JournalFieldDescriptions
{
    internal static Dictionary<string, string> FieldDescriptions { get; } = new()
    {
        { "_UID", "User ID of the process that wrote the log." },
        { "_GID", "Group ID of the process that wrote the log." },
        { "_COMM", "The name of the executable process." },
        { "_EXE", "The absolute path to the executable process." },
        { "_SYSLOG_IDENTIFIER", "The name of the program that wrote the log." },
        { "PRIORITY", "Log level (0=EMERG, 1=ALERT, 2=CRIT, 3=ERR, 4=WARNING, 5=NOTICE, 6=INFO, 7=DEBUG)" }
    };
}
