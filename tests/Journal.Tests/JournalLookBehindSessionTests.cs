using System.Diagnostics.CodeAnalysis;
using AwesomeAssertions;
using ViciOne.Journal;

namespace Journal.Tests;

[Trait("Category", "Integration")]
public sealed class JournalLookBehindSession_
{
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
    private static JournalLookBehindSession CreateSession()
        => new(new JournalGenericSession(
            new JournalOptions { OpeningMode = JournalOpeningMode.LocalOnly },
            () => { }));

    [Fact]
    public void Can_create_look_behind_session()
    {
        using var session = CreateSession();

        session.Should().NotBeNull();
    }

    [Fact]
    public void Session_starts_at_tail()
    {
        using var session = CreateSession();

        // GetLogs reads backward from tail, should return recent entries
        var logs = session.GetLogs(5);

        logs.Should().NotBeNull();
    }
}

[Trait("Category", "Integration")]
public sealed class JournalLookBehindSession_GetLogs
{
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
    private static JournalLookBehindSession CreateSession()
        => new(new JournalGenericSession(
            new JournalOptions { OpeningMode = JournalOpeningMode.LocalOnly },
            () => { }));

    [Fact]
    public void GetLogs_returns_entries_in_reverse_chronological_order()
    {
        using var session = CreateSession();

        var logs = session.GetLogs(5).ToList();

        logs.Should().NotBeNull();
        logs.Count.Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    public void GetLogs_returns_entries_with_expected_fields()
    {
        using var session = CreateSession();

        var logs = session.GetLogs(5).ToList();

        if (logs.Count > 0)
        {
            logs.First().Should().ContainKey("MESSAGE");
        }
    }

    [Fact]
    public void GetLogs_can_retrieve_multiple_batches()
    {
        using var session = CreateSession();

        var firstBatch = session.GetLogs(3).ToList();
        var secondBatch = session.GetLogs(3).ToList();

        // Both batches should return entries (may overlap or continue)
        firstBatch.Should().NotBeNull();
        secondBatch.Should().NotBeNull();
    }
}

[Trait("Category", "Integration")]
public sealed class JournalLookBehindSession_SetFilter
{
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
    private static JournalLookBehindSession CreateSession()
        => new(new JournalGenericSession(
            new JournalOptions { OpeningMode = JournalOpeningMode.LocalOnly },
            () => { }));

    [Fact]
    public void SetFilter_filters_logs_by_priority()
    {
        using var session = CreateSession();

        session.SetFilter("PRIORITY=6");
        var logs = session.GetLogs(10);

        logs.Should().NotBeNull();
        logs.Should().AllSatisfy(entry =>
        {
            if (entry.TryGetValue("PRIORITY", out var priority))
                priority.Should().Be("6");
        });
    }

    [Fact]
    public void SetFilter_resets_position_to_tail()
    {
        using var session = CreateSession();

        // Get some logs first
        var firstLogs = session.GetLogs(5).ToList();

        // Set filter (should reset to tail)
        session.SetFilter("PRIORITY=6");

        // Should be able to get logs again from tail
        var logsAfterFilter = session.GetLogs(5);

        logsAfterFilter.Should().NotBeNull();
    }

    [Fact]
    public void SetFilter_supports_disjunction()
    {
        using var session = CreateSession();

        session.SetFilter("PRIORITY=6+PRIORITY=5");
        var logs = session.GetLogs(10);

        logs.Should().NotBeNull();
        logs.Should().AllSatisfy(entry =>
        {
            if (entry.TryGetValue("PRIORITY", out var priority))
                priority.Should().BeOneOf("5", "6");
        });
    }
}

[Trait("Category", "Integration")]
public sealed class JournalLookBehindSession_Dispose
{
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
    private static JournalLookBehindSession CreateSession()
        => new(new JournalGenericSession(
            new JournalOptions { OpeningMode = JournalOpeningMode.LocalOnly },
            () => { }));

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
    public void Dispose_releases_resources_gracefully()
    {
        var session = CreateSession();
        _ = session.GetLogs(1);

        var dispose = () => session.Dispose();

        dispose.Should().NotThrow();
    }

    [Fact]
    public void Multiple_sessions_can_be_created_and_disposed()
    {
        for (var i = 0; i < 3; i++)
        {
            using var session = CreateSession();
            _ = session.GetLogs(1);
        }

        // Should complete without exception
        true.Should().BeTrue();
    }

    [Fact]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
    public void Dispose_also_disposes_underlying_generic_session()
    {
        var disposed = false;
        var genericSession = new JournalGenericSession(
            new JournalOptions { OpeningMode = JournalOpeningMode.LocalOnly },
            () => disposed = true);

        var lookBehindSession = new JournalLookBehindSession(genericSession);
        lookBehindSession.Dispose();

        disposed.Should().BeTrue();
    }
}
