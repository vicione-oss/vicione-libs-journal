using System.Globalization;
using AwesomeAssertions;
using ViciOne.Journal;

namespace Journal.Tests;

[Trait("Category", "Integration")]
public sealed class JournalGenericSession_
{
    private static JournalGenericSession CreateSession()
        => new(() => new SystemdJournal(), () => { });

    [Fact]
    public void Can_create_generic_session()
    {
        using var session = CreateSession();

        session.Should().NotBeNull();
    }

    [Fact]
    public void SeekHead_allows_reading_logs()
    {
        using var session = CreateSession();

        session.SeekHead();
        var logs = session.GetLogs(5);

        logs.Should().NotBeNull();
        logs.Count().Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    public void SeekTail_allows_reading_logs_backward()
    {
        using var session = CreateSession();

        session.SeekTail();
        var logs = session.GetLogs(5, forward: false);

        logs.Should().NotBeNull();
        logs.Count().Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    public void SeekRealTime_positions_journal_at_specified_time()
    {
        using var session = CreateSession();

        var seekTime = DateTime.UtcNow.AddMinutes(-5);
        session.SeekRealTime(seekTime);

        var logs = session.GetLogs(5);
        logs.Should().NotBeNull();
    }

    [Fact]
    public void GetLogs_with_invalid_size_throws_ArgumentOutOfRangeException()
    {
        using var session = CreateSession();
        session.SeekHead();

        var act = () => session.GetLogs(0);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("size");
    }

    [Fact]
    public void GetLogs_without_position_set_throws_InvalidOperationException()
    {
        using var session = CreateSession();

        // SetFilter resets position
        session.SetFilter("PRIORITY=6");

        var act = () => session.GetLogs(5);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Position not set*");
    }
}

[Trait("Category", "Integration")]
public sealed class JournalGenericSession_GetCursor
{
    private static JournalGenericSession CreateSession()
        => new(() => new SystemdJournal(), () => { });

    [Fact]
    public void GetCursor_returns_valid_cursor_string()
    {
        using var session = CreateSession();
        session.SeekHead();
        session.GetLogs(1); // Move to first entry

        var cursor = session.GetCursor();

        cursor.Should().NotBeNullOrEmpty();
        cursor.Should().StartWith("s=");
    }

    [Fact]
    public void SeekCursor_restores_position()
    {
        using var session = CreateSession();

        session.SeekHead();
        var firstLogs = session.GetLogs(3);
        var cursor = session.GetCursor();

        // Move somewhere else
        session.SeekTail();

        // Seek back to cursor position
        session.SeekCursor(cursor);
        var logsAfterSeek = session.GetLogs(3);

        logsAfterSeek.Should().NotBeNull();
    }
}

[Trait("Category", "Integration")]
public sealed class JournalGenericSession_SetFilter
{
    private static JournalGenericSession CreateSession()
        => new(() => new SystemdJournal(), () => { });

    [Fact]
    public void SetFilter_filters_logs_by_priority()
    {
        using var session = CreateSession();

        session.SetFilter("PRIORITY=6");
        session.SeekHead();

        var logs = session.GetLogs(10);

        logs.Should().NotBeNull();
        logs.Should().AllSatisfy(entry =>
        {
            if (entry.TryGetValue("PRIORITY", out var priority))
                priority.Should().Be("6");
        });
    }

    [Fact]
    public void SetFilter_supports_multiple_conditions()
    {
        using var session = CreateSession();

        session.SetFilter("PRIORITY=6,_TRANSPORT=journal");
        session.SeekHead();

        var logs = session.GetLogs(10);

        logs.Should().NotBeNull();
        logs.Should().AllSatisfy(entry =>
        {
            if (entry.TryGetValue("PRIORITY", out var priority))
                priority.Should().Be("6");
            if (entry.TryGetValue("_TRANSPORT", out var transport))
                transport.Should().Be("journal");
        });
    }

    [Fact]
    public void SetFilter_supports_disjunction()
    {
        using var session = CreateSession();

        session.SetFilter("PRIORITY=6+PRIORITY=5");
        session.SeekHead();

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
public sealed class JournalGenericSession_GetLogs
{
    private static JournalGenericSession CreateSession()
        => new(() => new SystemdJournal(), () => { });

    [Fact]
    public void Returns_entries_with_expected_fields()
    {
        using var session = CreateSession();

        session.SeekHead();
        var logs = session.GetLogs(5);

        logs.Should().NotBeEmpty("journal should contain at least one entry");
        logs.First().Should().ContainKey("MESSAGE");
    }

    [Fact]
    public void Forward_returns_entries_in_chronological_order()
    {
        using var session = CreateSession();

        session.SeekHead();
        var logs = session.GetLogs(5, forward: true).ToList();

        if (logs.Count >= 2)
        {
            var first = logs.First();
            var last = logs.Last();

            if (first.TryGetValue("__REALTIME_TIMESTAMP", out var firstTs) &&
                last.TryGetValue("__REALTIME_TIMESTAMP", out var lastTs))
            {
                long.Parse(firstTs, CultureInfo.InvariantCulture).Should().BeLessThanOrEqualTo(long.Parse(lastTs, CultureInfo.InvariantCulture));
            }
        }
    }

    [Fact]
    public void Backward_returns_entries_in_reverse_chronological_order()
    {
        using var session = CreateSession();

        session.SeekTail();
        var logs = session.GetLogs(5, forward: false).ToList();

        if (logs.Count >= 2)
        {
            var first = logs.First();
            var last = logs.Last();

            if (first.TryGetValue("__REALTIME_TIMESTAMP", out var firstTs) &&
                last.TryGetValue("__REALTIME_TIMESTAMP", out var lastTs))
            {
                long.Parse(firstTs, CultureInfo.InvariantCulture).Should().BeGreaterThanOrEqualTo(long.Parse(lastTs, CultureInfo.InvariantCulture));
            }
        }
    }

    [Fact]
    public void SkipFirst_skips_current_entry()
    {
        using var session = CreateSession();

        session.SeekHead();

        // Get first batch with first entry
        var logsWithFirst = session.GetLogs(3, forward: true, skipFirst: false).ToList();

        // Reset and get with skipFirst
        session.SeekHead();
        var logsSkipped = session.GetLogs(3, forward: true, skipFirst: true).ToList();

        if (logsWithFirst.Count >= 2 && logsSkipped.Count >= 1)
        {
            var secondEntry = logsWithFirst.ElementAtOrDefault(1);
            var firstSkipped = logsSkipped.FirstOrDefault();

            if (secondEntry != null && firstSkipped != null &&
                secondEntry.TryGetValue("__CURSOR", out var secondCursor) &&
                firstSkipped.TryGetValue("__CURSOR", out var skippedCursor))
            {
                skippedCursor.Should().Be(secondCursor);
            }
        }
    }
}

[Trait("Category", "Integration")]
public sealed class JournalGenericSession_Dispose
{
    private static JournalGenericSession CreateSession()
        => new(() => new SystemdJournal(), () => { });

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
    public void Dispose_releases_resources_gracefully()
    {
        var session = CreateSession();

        session.SeekHead();
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
            session.SeekHead();
            _ = session.GetLogs(1);
        }

        // Should complete without exception
        true.Should().BeTrue();
    }

    [Fact]
    public void OnDispose_callback_is_invoked()
    {
        var callbackInvoked = false;
        var session = new JournalGenericSession(
            () => new SystemdJournal(),
            () => callbackInvoked = true);

        session.Dispose();

        callbackInvoked.Should().BeTrue();
    }
}

public sealed class JournalGenericSession_SeekRealTime
{
    [Fact]
    public void Reports_a_time_before_1970_as_a_seek_error_in_any_time_zone()
    {
        using var session = new JournalGenericSession(() => new FakeSystemdJournal([]), () => { });

        var act = () => session.SeekRealTime(DateTime.MinValue);

        act.Should().Throw<InvalidOperationException>().WithMessage("Error seeking journal realtime.");
    }
}

public sealed class JournalGenericSession_SetFilter_Replacing
{
    [Fact]
    public void Reads_only_the_entries_of_the_last_filter()
    {
        List<FakeJournalEntry> entries =
        [
            FakeJournalEntry.At(DateTimeOffset.UnixEpoch.AddDays(1), "MESSAGE=a", "_SYSTEMD_UNIT=a.service"),
            FakeJournalEntry.At(DateTimeOffset.UnixEpoch.AddDays(2), "MESSAGE=b", "_SYSTEMD_UNIT=b.service"),
        ];
        using var session = new JournalGenericSession(() => new FakeSystemdJournal(entries), () => { });
        session.SetFilter("_SYSTEMD_UNIT=a.service");

        session.SetFilter("_SYSTEMD_UNIT=b.service");
        session.SeekHead();

        session.GetLogs(10).Select(l => l["MESSAGE"]).Should().Equal("b");
    }
}
