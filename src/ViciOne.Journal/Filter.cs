namespace ViciOne.Journal;

internal static class Filter
{
    internal static void Apply(string filter, ISystemdJournal journal)
        => Apply(filter, journal.AddMatch, journal.AddDisjunction);

    /// <param name="filter">
    /// Matches in the form <c>FIELD=value</c>: <c>,</c> joins matches into one group and <c>+</c> starts
    /// the next group. sd-journal ORs the groups, and within a group ORs matches on the same field
    /// and ANDs different fields.
    /// </param>
    internal static void Apply(string filter, Action<string> addMatch, Action addDisjunction)
    {
        if (string.IsNullOrEmpty(filter))
            return;

        const StringSplitOptions SplitOptions = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;
        var groups = filter.Split('+', SplitOptions);

        for (var i = 0; i < groups.Length; i++)
        {
            if (i > 0)
                addDisjunction();

            foreach (var match in groups[i].Split(',', SplitOptions))
                addMatch(match);
        }
    }
}
