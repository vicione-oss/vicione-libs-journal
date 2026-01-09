using Microsoft.Extensions.Logging;

namespace ViciOne.Journal;

/// <summary>
/// Provides a list of unique field values in the journal.
/// </summary>
internal static class JournalFieldValuesProvider
{
    public static List<string> GetFieldValues(ILogger<SystemdJournal> logger, string field)
    {
        using var journal = new SystemdJournal(logger, JournalOpeningMode.LocalOnly);
        return journal.EnumerateUniqueFieldValues(field);
    }
}
