namespace ViciOne.Journal;

internal static class Filter
{
    internal static void Apply(string filter, ISystemdJournal journal)
        => Apply(filter, journal.AddMatch, journal.AddDisjunction);

    internal static void Apply(string filter, Action<string> addMatch, Action addDisjunction)
    {
        if (string.IsNullOrEmpty(filter))
            return;

        var terms = filter.Split(['+', ','], StringSplitOptions.RemoveEmptyEntries);

        foreach (var term in terms)
        {
            addMatch(term.Trim());

            var index = filter.IndexOf(term, StringComparison.Ordinal) + term.Length;

            if (index < filter.Length && filter[index] == '+')
                addDisjunction();
        }
    }
}
