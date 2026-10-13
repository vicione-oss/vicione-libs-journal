using System.Text;

namespace ViciOne.Journal;

/// <summary>
/// A journal field name, encoded once for the native calls that read it.
/// </summary>
internal sealed class JournalField
{
    public JournalField(string name)
    {
        if (!SystemdJournal.FieldRegex().IsMatch(name))
            throw new ArgumentException($"Invalid field format: {name}. Only uppercase letters, digits, and underscores are allowed.", nameof(name));

        Name = name;
        NativeName = Encoding.UTF8.GetBytes(name + "\0");
    }

    public string Name { get; }

    /// <summary>
    /// The NUL-terminated UTF-8 name, as long as the <c>FIELD=</c> prefix of the field's data.
    /// </summary>
    public byte[] NativeName { get; }
}
