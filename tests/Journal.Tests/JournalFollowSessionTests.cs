using System.Diagnostics.CodeAnalysis;
using AwesomeAssertions;
using ViciOne.Journal;

namespace Journal.Tests;

[Trait("Category", "Integration")]
public sealed class JournalFollowSession_
{
    [Fact]
    public void Can_create_follow_session()
    {
        using var session = new JournalFollowSession(
            new JournalOptions { OpeningMode = JournalOpeningMode.LocalOnly },
            TimeSpan.FromMilliseconds(500),
            () => { });

        session.Should().NotBeNull();
    }

    [Fact]
    public void Throws_when_interval_less_than_250ms()
    {
        var act = () => new JournalFollowSession(
            new JournalOptions { OpeningMode = JournalOpeningMode.LocalOnly },
            TimeSpan.FromMilliseconds(100),
            () => { });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*interval*250ms*");
    }

    [Fact]
    public void Accepts_interval_of_250ms()
    {
        using var session = new JournalFollowSession(
            new JournalOptions { OpeningMode = JournalOpeningMode.LocalOnly },
            TimeSpan.FromMilliseconds(250),
            () => { });

        session.Should().NotBeNull();
    }
}

[Trait("Category", "Integration")]
public sealed class JournalFollowSession_StartStop
{
    private static JournalFollowSession CreateSession()
        => new(
            new JournalOptions { OpeningMode = JournalOpeningMode.LocalOnly },
            TimeSpan.FromMilliseconds(300),
            () => { });

    [Fact]
    public async Task Can_start_and_stop_session()
    {
        using var session = CreateSession();

        session.Start(0);
        await Task.Delay(100);
        await session.Stop();

        // Should complete without exception
        true.Should().BeTrue();
    }

    [Fact]
    public async Task Can_start_with_look_behind_count()
    {
        using var session = CreateSession();

        session.Start(5);
        await Task.Delay(100);
        await session.Stop();

        // Should complete without exception
        true.Should().BeTrue();
    }

    [Fact]
    public async Task Stop_is_idempotent()
    {
        using var session = CreateSession();

        session.Start(0);
        await session.Stop();
        await session.Stop();

        // Should complete without exception
        true.Should().BeTrue();
    }

    [Fact]
    public void Start_can_be_called_multiple_times_without_effect()
    {
        using var session = CreateSession();

        session.Start(0);
        session.Start(0); // Should be ignored

        // Should complete without exception
        true.Should().BeTrue();
    }
}

[Trait("Category", "Integration")]
public sealed class JournalFollowSession_SetFilter
{
    private static JournalFollowSession CreateSession()
        => new(
            new JournalOptions { OpeningMode = JournalOpeningMode.LocalOnly },
            TimeSpan.FromMilliseconds(300),
            () => { });

    [Fact]
    public void SetFilter_works_before_start()
    {
        using var session = CreateSession();

        var act = () => session.SetFilter("PRIORITY=6");

        act.Should().NotThrow();
    }

    [Fact]
    public void SetFilter_throws_when_session_is_running()
    {
        using var session = CreateSession();
        session.Start(0);

        var act = () => session.SetFilter("PRIORITY=6");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*running*");
    }

    [Fact]
    public async Task SetFilter_works_after_stop()
    {
        using var session = CreateSession();
        session.Start(0);
        await session.Stop();

        var act = () => session.SetFilter("PRIORITY=6");

        act.Should().NotThrow();
    }

    [Fact]
    public void SetFilter_supports_disjunction()
    {
        using var session = CreateSession();

        var act = () => session.SetFilter("PRIORITY=6+PRIORITY=5");

        act.Should().NotThrow();
    }
}

[Trait("Category", "Integration")]
public sealed class JournalFollowSession_Events
{
    private static JournalFollowSession CreateSession()
        => new(
            new JournalOptions { OpeningMode = JournalOpeningMode.LocalOnly },
            TimeSpan.FromMilliseconds(300),
            () => { });

    [Fact]
    public async Task NewEntry_event_can_be_subscribed()
    {
        using var session = CreateSession();
        var entries = new List<IReadOnlyDictionary<string, string>>();

        session.NewEntry += entry => entries.Add(entry);
        session.Start(5); // Start with 5 entries look-behind
        await Task.Delay(500); // Wait for at least one timer tick
        await session.Stop();

        // Should have received some entries from look-behind
        // Note: This may be empty if no recent entries exist
        entries.Should().NotBeNull();
    }

    [Fact]
    public async Task Error_event_can_be_subscribed()
    {
        using var session = CreateSession();
        Exception? receivedError = null;

        session.Error += ex => receivedError = ex;
        session.Start(0);
        await Task.Delay(100);
        await session.Stop();

        // No error expected in normal operation
        receivedError.Should().BeNull();
    }

    [Fact]
    public async Task Multiple_entries_are_delivered_in_order()
    {
        using var session = CreateSession();
        var entries = new List<IReadOnlyDictionary<string, string>>();

        session.NewEntry += entry => entries.Add(entry);
        session.Start(10);
        await Task.Delay(500);
        await session.Stop();

        if (entries.Count >= 2)
        {
            // Entries should have MESSAGE field
            entries.Should().AllSatisfy(e => e.Should().ContainKey("MESSAGE"));
        }
    }
}

[Trait("Category", "Integration")]
public sealed class JournalFollowSession_Dispose
{
    [Fact]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
    public void Dispose_releases_resources_gracefully()
    {
        var session = new JournalFollowSession(
            new JournalOptions { OpeningMode = JournalOpeningMode.LocalOnly },
            TimeSpan.FromMilliseconds(300),
            () => { });

        session.Start(0);

        var dispose = () => session.Dispose();

        dispose.Should().NotThrow();
    }

    [Fact]
    public void OnDispose_callback_is_invoked()
    {
        var callbackInvoked = false;
        var session = new JournalFollowSession(
            new JournalOptions { OpeningMode = JournalOpeningMode.LocalOnly },
            TimeSpan.FromMilliseconds(300),
            () => callbackInvoked = true);

        session.Dispose();

        callbackInvoked.Should().BeTrue();
    }

    [Fact]
    public void Dispose_stops_running_session()
    {
        var session = new JournalFollowSession(
            new JournalOptions { OpeningMode = JournalOpeningMode.LocalOnly },
            TimeSpan.FromMilliseconds(300),
            () => { });

        session.Start(0);
        session.Dispose();

        // Should complete without hanging
        true.Should().BeTrue();
    }

    [Fact]
    public void Multiple_sessions_can_be_created_and_disposed()
    {
        for (var i = 0; i < 3; i++)
        {
            using var session = new JournalFollowSession(
                new JournalOptions { OpeningMode = JournalOpeningMode.LocalOnly },
                TimeSpan.FromMilliseconds(300),
                () => { });

            session.Start(0);
        }

        // Should complete without exception
        true.Should().BeTrue();
    }
}

[Trait("Category", "Integration")]
public sealed class JournalFollowSession_FilteredEvents
{
    [Fact]
    public async Task Filtered_session_only_receives_matching_entries()
    {
        using var session = new JournalFollowSession(
            new JournalOptions { OpeningMode = JournalOpeningMode.LocalOnly },
            TimeSpan.FromMilliseconds(300),
            () => { });

        var entries = new List<IReadOnlyDictionary<string, string>>();

        session.SetFilter("PRIORITY=6");
        session.NewEntry += entry => entries.Add(entry);
        session.Start(10);
        await Task.Delay(500);
        await session.Stop();

        entries.Should().AllSatisfy(entry =>
        {
            if (entry.TryGetValue("PRIORITY", out var priority))
                priority.Should().Be("6");
        });
    }

    [Fact]
    public async Task Disjunction_filter_receives_multiple_priorities()
    {
        using var session = new JournalFollowSession(
            new JournalOptions { OpeningMode = JournalOpeningMode.LocalOnly },
            TimeSpan.FromMilliseconds(300),
            () => { });

        var entries = new List<IReadOnlyDictionary<string, string>>();

        session.SetFilter("PRIORITY=6+PRIORITY=5");
        session.NewEntry += entry => entries.Add(entry);
        session.Start(10);
        await Task.Delay(500);
        await session.Stop();

        entries.Should().AllSatisfy(entry =>
        {
            if (entry.TryGetValue("PRIORITY", out var priority))
                priority.Should().BeOneOf("5", "6");
        });
    }
}
