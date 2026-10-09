using AwesomeAssertions;
using ViciOne.Journal;

namespace Journal.Tests;

public class SystemdJournal_FieldRegex
{
    [Theory]
    [InlineData("FIELD")]
    [InlineData("FIELD1")]
    [InlineData("_FIELD")]
    [InlineData("_FIELD1")]
    [InlineData("FIELD_1")]
    public void Matches_allowed_field_names(string fieldName)
        => SystemdJournal.FieldRegex().Match(fieldName).Success.Should().BeTrue();

    [Theory]
    [InlineData("_Field_1")]
    [InlineData("__FIELD")]
    [InlineData("field")]
    [InlineData("FIELD__1")]
    public void Does_not_match_unallowed_field_names(string fieldName)
        => SystemdJournal.FieldRegex().Match(fieldName).Success.Should().BeFalse();
}

[Trait("Category", "Integration")]
public class SystemdJournal_AddMatch
{
    [Fact]
    public void Matches_a_quoted_value_that_contains_spaces()
    {
        var message = FindRecentMessageWithSpace();
        using var journal = new SystemdJournal();

        journal.AddMatch($"MESSAGE=\"{message}\"");
        journal.SeekTail();

        journal.Previous().Should().BeTrue();
        journal.GetCurrentEntry()["MESSAGE"].Should().Be(message);
    }

    private static string FindRecentMessageWithSpace()
    {
        using var journal = new SystemdJournal();
        journal.SeekTail();

        while (journal.Previous())
        {
            var message = journal.GetCurrentEntry().GetValueOrDefault("MESSAGE");
            if (message is not null && message.Contains(' ', StringComparison.Ordinal) && !message.Contains('"', StringComparison.Ordinal))
                return message;
        }

        throw new InvalidOperationException("The journal has no message with a space.");
    }
}
