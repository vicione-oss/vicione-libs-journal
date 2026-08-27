using System.Globalization;

namespace ViciOne.Journal;

internal sealed class SpamKey
{
    private readonly string?[] _values;
    private readonly int _hashCode;

    private SpamKey(string?[] values, int hashCode)
    {
        _values = values;
        _hashCode = hashCode;
    }

    // requireAllFields: when set, an entry that doesn't carry every field produces no key at all.
    // Set for explicitly configured AggregationFields; the built-in default set tolerates absence
    // because MESSAGE and PRIORITY are always present.
    internal static SpamKey? Create(IReadOnlyDictionary<string, object?> entry, string[] fields, bool requireAllFields)
    {
        if (fields.Length == 0)
            return null;

        var values = new string?[fields.Length];
        var hash = new HashCode();

        for (var i = 0; i < fields.Length; i++)
        {
            entry.TryGetValue(fields[i], out var value);
            var canonical = Canonicalize(value);

            // A configured field the entry doesn't carry leaves the key without the discriminator
            // the operator asked for - every such entry canonicalizes to the same null and lands
            // in one bucket, so unrelated entries would be suppressed as if they were repeats.
            // Absent and present-but-empty are treated alike; neither discriminates.
            if (requireAllFields && string.IsNullOrEmpty(canonical))
                return null;

            values[i] = canonical;
            hash.Add(canonical, StringComparer.Ordinal);
        }

        return new SpamKey(values, hash.ToHashCode());
    }

    public override bool Equals(object? obj)
        => obj is SpamKey other && Equals(other);

    private bool Equals(SpamKey other)
    {
        if (_hashCode != other._hashCode || _values.Length != other._values.Length)
            return false;

        for (var i = 0; i < _values.Length; i++)
        {
            if (!string.Equals(_values[i], other._values[i], StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    public override int GetHashCode() => _hashCode;

    private static string? Canonicalize(object? value)
        => value switch
        {
            null => null,
            string text => text,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };
}
