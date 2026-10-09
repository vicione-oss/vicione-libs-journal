namespace ViciOne.Journal;

/// <summary>
/// The fields to read from each entry. Create it once and reuse it: the names are encoded for the native calls only here.
/// </summary>
public sealed class JournalFieldSet
{
    private readonly Dictionary<string, int> _indices;

    /// <exception cref="ArgumentException">Thrown when a name is not a valid journal field name or occurs twice.</exception>
    public JournalFieldSet(params IEnumerable<string> names)
    {
        Fields = [.. names.Select(n => new JournalField(n))];
        Names = [.. Fields.Select(f => f.Name)];
        _indices = Names.Select((name, index) => (name, index)).ToDictionary(f => f.name, f => f.index);
    }

    public IReadOnlyList<string> Names { get; }

    internal IReadOnlyList<JournalField> Fields { get; }

    /// <returns>The index of the field, or -1 if it is not part of this field set.</returns>
    internal int IndexOf(ReadOnlySpan<byte> name)
    {
        for (var i = 0; i < Fields.Count; i++)
        {
            if (Fields[i].NativeName.AsSpan(..^1).SequenceEqual(name))
                return i;
        }

        return -1;
    }

    internal int IndexOf(string name)
        => _indices.TryGetValue(name, out var index)
            ? index
            : throw new ArgumentException($"The field {name} is not part of this field set.", nameof(name));
}
