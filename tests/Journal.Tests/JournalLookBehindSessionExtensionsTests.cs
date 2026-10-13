using AwesomeAssertions;
using ViciOne.Journal;

namespace Journal.Tests;

[Trait("Category", "Integration")]
public sealed class JournalLookBehindSessionExtensions_ToLookBehind
{
    [Fact]
    public void ToLookBehind_extension_creates_session_from_generic()
    {
        using var genericSession = new JournalGenericSession(
            () => new SystemdJournal(),
            () => { });

        using var lookBehindSession = genericSession.ToLookBehind();

        lookBehindSession.Should().NotBeNull();
        lookBehindSession.Should().BeOfType<JournalLookBehindSession>();
    }

    [Fact]
    public void ToLookBehind_session_can_retrieve_logs()
    {
        using var genericSession = new JournalGenericSession(
            () => new SystemdJournal(),
            () => { });

        using var lookBehindSession = genericSession.ToLookBehind();
        var logs = lookBehindSession.GetLogs(5);

        logs.Should().NotBeNull();
    }
}
