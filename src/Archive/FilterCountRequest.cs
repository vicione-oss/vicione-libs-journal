namespace ViciOne.Journal;

internal sealed class FilterCountRequest
{
    public List<FilterGroup> Filters { get; internal set; } = [];
}
