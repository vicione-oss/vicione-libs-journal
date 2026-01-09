namespace ViciOne.Journal;

internal sealed class SpamKeyComparer : IEqualityComparer<SpamKey>
{
    internal static SpamKeyComparer Instance { get; } = new();

    public bool Equals(SpamKey? x, SpamKey? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || y is null)
            return false;

        return x.Equals(y);
    }

    public int GetHashCode(SpamKey obj) => obj.GetHashCode();
}
