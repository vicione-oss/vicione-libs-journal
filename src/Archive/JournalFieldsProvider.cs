using Microsoft.Extensions.Logging.Abstractions;

namespace ViciOne.Journal;

/// <summary>
/// Provides a list of available fields in the journal.
/// </summary>
/// TODO: try to cache the fields
internal static class JournalFieldsProvider
{
    private static readonly Lazy<List<string>> s_cachedFields = new(() =>
    {
        using var journal = new SystemdJournal(new NullLogger<SystemdJournal>(), JournalOpeningMode.LocalOnly);
        return journal.EnumerateAvailableFields();
    });

    public static List<string> GetAvailableFields() => s_cachedFields.Value;
}
