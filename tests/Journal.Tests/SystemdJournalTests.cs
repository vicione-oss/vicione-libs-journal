using FluentAssertions;
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
