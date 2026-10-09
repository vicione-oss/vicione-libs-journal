using System.Collections.Concurrent;
using System.Globalization;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ViciOne.Journal;

namespace Journal.Tests;

public class JournalService_GetLogs
{
    private readonly List<FakeJournalEntry> _entries =
    [
        FakeJournalEntry.At(At(0), "MESSAGE=first", "_SYSTEMD_UNIT=a.service"),
        FakeJournalEntry.At(At(1), "MESSAGE=second", "_SYSTEMD_UNIT=b.service"),
        FakeJournalEntry.At(At(2), "MESSAGE=third", "_SYSTEMD_UNIT=a.service"),
        FakeJournalEntry.At(At(3), "MESSAGE=fourth", "_SYSTEMD_UNIT=b.service"),
    ];

    private static DateTimeOffset At(int minute)
        => new(new DateTime(2026, 1, 15, 12, minute, 0, DateTimeKind.Local));

    private JournalService CreateService()
        => new(() => new FakeSystemdJournal(_entries));

    [Fact]
    public async Task Returns_the_entries_from_since_in_ascending_order_Async()
    {
        using var service = CreateService();

        var logs = await service.GetLogs(At(1), "asc", cancellationToken: TestContext.Current.CancellationToken);

        logs.Select(l => l["MESSAGE"]).Should().Equal("second", "third", "fourth");
    }

    [Fact]
    public async Task Returns_the_first_entries_from_since_newest_first_in_descending_order_Async()
    {
        using var service = CreateService();

        var logs = await service.GetLogs(At(1), "desc", limit: 2, cancellationToken: TestContext.Current.CancellationToken);

        logs.Select(l => l["MESSAGE"]).Should().Equal("third", "second");
    }

    [Fact]
    public async Task Throws_OperationCanceledException_when_cancelled_while_reading_Async()
    {
        using CancellationTokenSource cancellation = new();
        using var service = new JournalService(() => new FakeSystemdJournal(_entries) { Stepped = cancellation.Cancel });

        var act = () => service.GetLogs(At(0), "asc", cancellationToken: cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData("DESC")]
    [InlineData("Desc")]
    public async Task Accepts_the_descending_order_in_any_case_Async(string order)
    {
        using var service = CreateService();

        var logs = await service.GetLogs(At(1), order, limit: 2, cancellationToken: TestContext.Current.CancellationToken);

        logs.Select(l => l["MESSAGE"]).Should().Equal("third", "second");
    }

    [Fact]
    public async Task Reads_from_the_instant_of_since_whatever_its_offset_Async()
    {
        using var service = CreateService();
        var chathamIslands = TimeSpan.FromHours(13.75);

        var logs = await service.GetLogs(At(1).ToOffset(chathamIslands), "asc", cancellationToken: TestContext.Current.CancellationToken);

        logs.Select(l => l["MESSAGE"]).Should().Equal("second", "third", "fourth");
    }

    [Fact]
    public async Task Returns_only_entries_matching_the_filter_Async()
    {
        using var service = CreateService();

        var logs = await service.GetLogs(At(0), "asc", filter: "_SYSTEMD_UNIT=a.service", cancellationToken: TestContext.Current.CancellationToken);

        logs.Select(l => l["MESSAGE"]).Should().Equal("first", "third");
    }
}

public class JournalService_GetPriorityCounts
{
    private static readonly DateTimeOffset s_start = new(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Counts_each_priority_of_an_interval_with_entries_Async()
    {
        List<FakeJournalEntry> entries =
        [
            FakeJournalEntry.At(s_start, "PRIORITY=3"),
            FakeJournalEntry.At(s_start.AddMinutes(1), "PRIORITY=3"),
            FakeJournalEntry.At(s_start.AddMinutes(2), "PRIORITY=6"),
            FakeJournalEntry.At(s_start.AddMinutes(3), "PRIORITY=4"),
        ];
        using var service = new JournalService(() => new FakeSystemdJournal(entries));

        var counts = await service.GetPriorityCounts(s_start, s_start.AddMinutes(3), cancellationToken: TestContext.Current.CancellationToken);

        counts.Should().Equal(new Dictionary<int, int> { [0] = 0, [1] = 0, [2] = 0, [3] = 2, [4] = 0, [5] = 0, [6] = 1, [7] = 0 });
    }

    [Fact]
    public async Task Counts_each_priority_from_since_on_Async()
    {
        List<FakeJournalEntry> entries =
        [
            FakeJournalEntry.At(s_start, "PRIORITY=3"),
            FakeJournalEntry.At(s_start.AddMinutes(1), "PRIORITY=6"),
            FakeJournalEntry.At(s_start.AddMinutes(2), "PRIORITY=6"),
        ];
        using var service = new JournalService(() => new FakeSystemdJournal(entries));

        var counts = await service.GetPriorityCounts(s_start.AddMinutes(1), cancellationToken: TestContext.Current.CancellationToken);

        counts.Should().Equal(new Dictionary<int, int> { [0] = 0, [1] = 0, [2] = 0, [3] = 0, [4] = 0, [5] = 0, [6] = 2, [7] = 0 });
    }

    [Fact]
    public async Task Rejects_a_since_in_the_future_Async()
    {
        using var service = new JournalService(() => new FakeSystemdJournal([]));

        var act = () => service.GetPriorityCounts(DateTimeOffset.Now.AddMinutes(1), cancellationToken: TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("since");
    }

    [Fact]
    public async Task Throws_OperationCanceledException_when_cancelled_while_counting_Async()
    {
        using CancellationTokenSource cancellation = new();
        List<FakeJournalEntry> entries = [FakeJournalEntry.At(s_start, "PRIORITY=3"), FakeJournalEntry.At(s_start.AddMinutes(1), "PRIORITY=3")];
        using var service = new JournalService(() => new FakeSystemdJournal(entries) { Stepped = cancellation.Cancel });

        var act = () => service.GetPriorityCounts(s_start, s_start.AddMinutes(2), cancellationToken: cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}

public class JournalService_GetPriorityHistogram
{
    private static readonly DateTimeOffset s_start = new(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);

    private static JournalService CreateService(params FakeJournalEntry[] entries)
    {
        List<FakeJournalEntry> list = [.. entries];
        return new(() => new FakeSystemdJournal(list));
    }

    [Fact]
    public async Task Counts_entries_per_bucket_and_priority_Async()
    {
        using var service = CreateService(
            FakeJournalEntry.At(s_start.AddMinutes(-1), "PRIORITY=3"),
            FakeJournalEntry.At(s_start, "PRIORITY=3"),
            FakeJournalEntry.At(s_start.AddMinutes(9), "PRIORITY=6"),
            FakeJournalEntry.At(s_start.AddMinutes(10), "PRIORITY=3"),
            FakeJournalEntry.At(s_start.AddMinutes(19), "PRIORITY=3"),
            FakeJournalEntry.At(s_start.AddMinutes(20), "PRIORITY=3"));

        var histogram = await service.GetPriorityHistogram(s_start, s_start.AddMinutes(20), TimeSpan.FromMinutes(10), cancellationToken: TestContext.Current.CancellationToken);

        histogram.BucketCount.Should().Be(2);
        histogram.GetCount(0, 3).Should().Be(1);
        histogram.GetCount(0, 6).Should().Be(1);
        histogram.GetCount(1, 3).Should().Be(2);
    }

    [Fact]
    public async Task Counts_entries_of_the_range_that_follow_a_later_one_Async()
    {
        using var service = CreateService(
            FakeJournalEntry.At(s_start, "PRIORITY=3"),
            FakeJournalEntry.At(s_start.AddHours(2), "PRIORITY=3"),
            FakeJournalEntry.At(s_start.AddMinutes(1), "PRIORITY=3"));

        var histogram = await service.GetPriorityHistogram(s_start, s_start.AddMinutes(10), TimeSpan.FromMinutes(10), cancellationToken: TestContext.Current.CancellationToken);

        histogram.GetCount(0, 3).Should().Be(2);
    }

    [Fact]
    public async Task Stops_after_a_run_of_entries_past_the_end_of_the_range_Async()
    {
        using var service = CreateService(
        [
            .. Enumerable.Range(0, 1_000).Select(_ => FakeJournalEntry.At(s_start.AddHours(2), "PRIORITY=3")),
            FakeJournalEntry.At(s_start.AddMinutes(1), "PRIORITY=3"),
        ]);

        var histogram = await service.GetPriorityHistogram(s_start, s_start.AddMinutes(10), TimeSpan.FromMinutes(10), cancellationToken: TestContext.Current.CancellationToken);

        histogram.GetCount(0, 3).Should().Be(0);
    }

    [Fact]
    public async Task Ends_the_last_bucket_at_the_end_of_the_range_Async()
    {
        using var service = CreateService();

        var histogram = await service.GetPriorityHistogram(s_start, s_start.AddMinutes(25), TimeSpan.FromMinutes(10), cancellationToken: TestContext.Current.CancellationToken);

        histogram.BucketEdges.Should().Equal(s_start, s_start.AddMinutes(10), s_start.AddMinutes(20), s_start.AddMinutes(25));
    }

    [Fact]
    public async Task Groups_by_unit_and_falls_back_to_the_syslog_identifier_Async()
    {
        using var service = CreateService(
            FakeJournalEntry.At(s_start, "PRIORITY=3", "_SYSTEMD_UNIT=a.service", "SYSLOG_IDENTIFIER=a"),
            FakeJournalEntry.At(s_start, "PRIORITY=3", "SYSLOG_IDENTIFIER=kernel"),
            FakeJournalEntry.At(s_start, "PRIORITY=4", "_SYSTEMD_UNIT=a.service"),
            FakeJournalEntry.At(s_start, "PRIORITY=4"));

        var histogram = await service.GetPriorityHistogram(s_start, s_start.AddMinutes(1), TimeSpan.FromMinutes(1), cancellationToken: TestContext.Current.CancellationToken);

        histogram.Groups.Should().Equal("a.service", "kernel", "");
        histogram.GetCount(0, 3, 0).Should().Be(1);
        histogram.GetCount(0, 4, 0).Should().Be(1);
        histogram.GetCount(0, 3, 1).Should().Be(1);
        histogram.GetCount(0, 4, 2).Should().Be(1);
    }

    [Fact]
    public async Task Counts_all_entries_in_one_group_without_grouping_Async()
    {
        using var service = CreateService(
            FakeJournalEntry.At(s_start, "PRIORITY=3", "_SYSTEMD_UNIT=a.service"),
            FakeJournalEntry.At(s_start, "PRIORITY=3", "_SYSTEMD_UNIT=b.service"));

        var histogram = await service.GetPriorityHistogram(s_start, s_start.AddMinutes(1), TimeSpan.FromMinutes(1), groupBy: null, cancellationToken: TestContext.Current.CancellationToken);

        histogram.Groups.Should().Equal("");
        histogram.GetCount(0, 3, 0).Should().Be(2);
    }

    [Theory]
    [InlineData("PRIORITY=8")]
    [InlineData("PRIORITY=x")]
    [InlineData("PRIORITY=13")]
    [InlineData("MESSAGE=no priority")]
    public async Task Skips_entries_without_a_valid_priority_Async(string field)
    {
        using var service = CreateService(FakeJournalEntry.At(s_start, field));

        var histogram = await service.GetPriorityHistogram(s_start, s_start.AddMinutes(1), TimeSpan.FromMinutes(1), cancellationToken: TestContext.Current.CancellationToken);

        histogram.Groups.Should().BeEmpty();
    }

    [Fact]
    public async Task Counts_only_entries_matching_the_filter_Async()
    {
        using var service = CreateService(
            FakeJournalEntry.At(s_start, "PRIORITY=3", "_SYSTEMD_UNIT=a.service"),
            FakeJournalEntry.At(s_start, "PRIORITY=3", "_SYSTEMD_UNIT=b.service"));

        var histogram = await service.GetPriorityHistogram(s_start, s_start.AddMinutes(1), TimeSpan.FromMinutes(1), filter: "_SYSTEMD_UNIT=b.service", cancellationToken: TestContext.Current.CancellationToken);

        histogram.Groups.Should().Equal("b.service");
    }

    [Fact]
    public async Task Places_entries_by_instant_when_the_edges_cross_a_daylight_saving_change_Async()
    {
        var cest = TimeSpan.FromHours(2);
        var cet = TimeSpan.FromHours(1);
        DateTimeOffset[] localHours =
        [
            new(2026, 10, 25, 1, 0, 0, cest),
            new(2026, 10, 25, 2, 0, 0, cest),
            new(2026, 10, 25, 2, 0, 0, cet),
            new(2026, 10, 25, 3, 0, 0, cet),
        ];
        using var service = CreateService(
            FakeJournalEntry.At(new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero), "PRIORITY=6"),
            FakeJournalEntry.At(new DateTimeOffset(2026, 10, 25, 0, 59, 0, TimeSpan.Zero), "PRIORITY=6"),
            FakeJournalEntry.At(new DateTimeOffset(2026, 10, 25, 1, 0, 0, TimeSpan.Zero), "PRIORITY=6"));

        var histogram = await service.GetPriorityHistogram(localHours, cancellationToken: TestContext.Current.CancellationToken);

        histogram.GetCount(0, 6).Should().Be(0);
        histogram.GetCount(1, 6).Should().Be(2);
        histogram.GetCount(2, 6).Should().Be(1);
    }

    [Fact]
    public async Task Stops_when_the_groups_would_exceed_the_counter_limit_Async()
    {
        using var service = CreateService([.. Enumerable.Range(0, 11).Select(i => FakeJournalEntry.At(s_start, "PRIORITY=6", $"_SYSTEMD_UNIT=unit{i}.service"))]);

        var act = () => service.GetPriorityHistogram(s_start, s_start.AddMinutes(50_000), TimeSpan.FromMinutes(1), cancellationToken: TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithInnerException<InvalidOperationException>().WithMessage("*counter limit*");
    }

    [Fact]
    public async Task Rejects_an_invalid_group_field_before_reading_Async()
    {
        using var service = CreateService();

        var act = () => service.GetPriorityHistogram(s_start, s_start.AddMinutes(1), TimeSpan.FromMinutes(1), groupBy: "unit", cancellationToken: TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("groupBy");
    }

    [Fact]
    public async Task Rejects_bucket_edges_before_1970_Async()
    {
        using var service = CreateService();

        var act = () => service.GetPriorityHistogram([DateTimeOffset.UnixEpoch.AddDays(-1), s_start], cancellationToken: TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Rejects_bucket_edges_that_are_not_increasing_Async()
    {
        using var service = CreateService();

        var act = () => service.GetPriorityHistogram([s_start, s_start], cancellationToken: TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}

public class JournalService_ctor
{
    [Theory]
    [InlineData(0, 1, nameof(JournalOptions.MaxSessions))]
    [InlineData(1, 0, nameof(JournalOptions.MaxConcurrentQueries))]
    public void Rejects_a_limit_below_one(int maxSessions, int maxConcurrentQueries, string option)
    {
        var act = () => new JournalService(() => new FakeSystemdJournal([]), new JournalOptions { MaxSessions = maxSessions, MaxConcurrentQueries = maxConcurrentQueries });

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(option);
    }
}

public class JournalService_CreateGeneric
{
    private readonly List<FakeJournalEntry> _entries = [];

    [Fact]
    public async Task Throws_a_busy_exception_when_the_session_limit_is_reached_Async()
    {
        using var service = new JournalService(() => new FakeSystemdJournal(_entries), new JournalOptions { MaxSessions = 1 });
        using var session = await service.CreateGeneric(TestContext.Current.CancellationToken);

        var act = () => service.CreateGeneric(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<JournalBusyException>();
    }

    [Fact]
    public async Task Releases_the_slot_when_the_journal_cannot_be_opened_Async()
    {
        var failToOpen = true;
        using var service = new JournalService(
            () => failToOpen ? throw new JournalException("open failed", -2) : new FakeSystemdJournal(_entries),
            new JournalOptions { MaxSessions = 1 });
        var failing = () => service.CreateGeneric(TestContext.Current.CancellationToken);
        await failing.Should().ThrowAsync<JournalException>();
        failToOpen = false;

        using var session = await service.CreateGeneric(TestContext.Current.CancellationToken);

        session.Should().NotBeNull();
    }

    [Fact]
    public async Task Leaves_the_query_slots_to_queries_Async()
    {
        using var service = new JournalService(() => new FakeSystemdJournal(_entries), new JournalOptions { MaxSessions = 1 });
        using var session = await service.CreateGeneric(TestContext.Current.CancellationToken);

        var fields = await service.GetFields(TestContext.Current.CancellationToken);

        fields.Should().BeEmpty();
    }
}

public class JournalService_CreateFollower
{
    [Fact]
    public async Task Delivers_entries_from_a_journal_file_created_after_start_Async()
    {
        List<FakeJournalEntry> entries = [];
        ConcurrentQueue<FakeJournalEntry> newFileEntries = [];
        using var service = new JournalService(() => new FakeSystemdJournal(entries, newFileEntries));
        using var follower = await service.CreateFollower(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
        TaskCompletionSource<string> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        follower.NewEntry += entry => received.TrySetResult(entry["MESSAGE"]);
        follower.Start(0);

        newFileEntries.Enqueue(FakeJournalEntry.At(DateTimeOffset.UtcNow, "MESSAGE=after rotation"));

        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        message.Should().Be("after rotation");
        await follower.Stop();
    }

    [Fact]
    public async Task Delivers_the_requested_number_of_recent_entries_on_start_Async()
    {
        List<FakeJournalEntry> entries =
        [
            FakeJournalEntry.At(DateTimeOffset.UnixEpoch.AddDays(1), "MESSAGE=first"),
            FakeJournalEntry.At(DateTimeOffset.UnixEpoch.AddDays(2), "MESSAGE=second"),
            FakeJournalEntry.At(DateTimeOffset.UnixEpoch.AddDays(3), "MESSAGE=third"),
        ];
        using var service = new JournalService(() => new FakeSystemdJournal(entries));
        using var follower = await service.CreateFollower(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
        ConcurrentQueue<string> messages = [];
        follower.NewEntry += entry => messages.Enqueue(entry["MESSAGE"]);

        follower.Start(2);
        await Task.Delay(TimeSpan.FromMilliseconds(750), TestContext.Current.CancellationToken);
        await follower.Stop();

        messages.Should().Equal("second", "third");
    }
}

public class JournalService_CreateFollower_Records
{
    private static readonly JournalFieldSet s_fields = new("MESSAGE");

    [Fact]
    public async Task Delivers_the_recent_and_then_the_new_entries_in_one_batch_per_interval_Async()
    {
        List<FakeJournalEntry> entries =
        [
            FakeJournalEntry.At(DateTimeOffset.UnixEpoch.AddDays(1), "MESSAGE=first", "PRIORITY=6"),
            FakeJournalEntry.At(DateTimeOffset.UnixEpoch.AddDays(2), "MESSAGE=second", "PRIORITY=4"),
        ];
        ConcurrentQueue<FakeJournalEntry> newFileEntries = [];
        using var service = new JournalService(() => new FakeSystemdJournal(entries, newFileEntries));
        using var follower = await service.CreateFollower(TimeSpan.FromMilliseconds(250), s_fields, TestContext.Current.CancellationToken);
        ConcurrentQueue<IReadOnlyList<JournalRecord>> batches = [];
        follower.NewRecords += batches.Enqueue;

        follower.Start(1);
        await Task.Delay(TimeSpan.FromMilliseconds(400), TestContext.Current.CancellationToken);
        newFileEntries.Enqueue(FakeJournalEntry.At(DateTimeOffset.UnixEpoch.AddDays(3), "MESSAGE=third", "PRIORITY=3"));
        newFileEntries.Enqueue(FakeJournalEntry.At(DateTimeOffset.UnixEpoch.AddDays(3), "MESSAGE=fourth", "PRIORITY=3"));
        await Task.Delay(TimeSpan.FromMilliseconds(400), TestContext.Current.CancellationToken);
        await follower.Stop();

        batches.Select(b => b.Select(r => r["MESSAGE"])).Should().BeEquivalentTo(
            [new[] { "second" }, ["third", "fourth"]],
            o => o.WithStrictOrdering());
        batches.First().Single().Priority.Should().Be(4);
    }
}

public class JournalService_ReadPage
{
    private static readonly DateTimeOffset s_start = new(2026, 4, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly JournalFieldSet s_fields = new("MESSAGE");

    private readonly List<FakeJournalEntry> _entries =
    [
        FakeJournalEntry.At(s_start, "MESSAGE=1", "_SYSTEMD_UNIT=a.service"),
        FakeJournalEntry.At(s_start.AddSeconds(1), "MESSAGE=2", "_SYSTEMD_UNIT=b.service"),
        FakeJournalEntry.At(s_start.AddSeconds(2), "MESSAGE=3", "_SYSTEMD_UNIT=a.service"),
        FakeJournalEntry.At(s_start.AddSeconds(2), "MESSAGE=4", "_SYSTEMD_UNIT=b.service"),
        FakeJournalEntry.At(s_start.AddSeconds(3), "MESSAGE=5", "_SYSTEMD_UNIT=a.service"),
    ];

    private async Task<JournalPage> ReadPageAsync(JournalPageRequest request)
    {
        using var service = new JournalService(() => new FakeSystemdJournal(_entries));
        return await service.ReadPage(request, TestContext.Current.CancellationToken);
    }

    private static IEnumerable<string?> Messages(JournalPage page)
        => page.Records.Select(r => r["MESSAGE"]);

    [Fact]
    public async Task Reads_the_newest_entries_oldest_first_by_default_Async()
    {
        var page = await ReadPageAsync(new(s_fields) { Count = 2 });

        Messages(page).Should().Equal("4", "5");
        page.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task Reads_the_oldest_entries_when_reading_newer_without_an_anchor_Async()
    {
        var page = await ReadPageAsync(new(s_fields) { Count = 2, Direction = JournalDirection.Newer });

        Messages(page).Should().Equal("1", "2");
    }

    [Fact]
    public async Task Reads_the_entries_older_than_a_cursor_Async()
    {
        var page = await ReadPageAsync(new(s_fields) { Count = 2, Cursor = _entries[3].Cursor });

        Messages(page).Should().Equal("2", "3");
        page.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task Reads_the_entries_newer_than_a_cursor_Async()
    {
        var page = await ReadPageAsync(new(s_fields) { Count = 10, Direction = JournalDirection.Newer, Cursor = _entries[2].Cursor });

        Messages(page).Should().Equal("4", "5");
        page.HasMore.Should().BeFalse();
    }

    [Theory]
    [InlineData(JournalDirection.Older, new[] { "1", "2" })]
    [InlineData(JournalDirection.Newer, new[] { "4", "5" })]
    public async Task Starts_next_to_a_cursor_whose_entry_is_gone_Async(JournalDirection direction, string[] expected)
    {
        var cursor = _entries[2].Cursor;
        _entries.RemoveAt(2);

        var page = await ReadPageAsync(new(s_fields) { Count = 2, Direction = direction, Cursor = cursor });

        Messages(page).Should().Equal(expected);
    }

    [Fact]
    public async Task Reads_the_entries_at_or_after_a_time_Async()
    {
        var page = await ReadPageAsync(new(s_fields) { Direction = JournalDirection.Newer, Time = s_start.AddSeconds(2) });

        Messages(page).Should().Equal("3", "4", "5");
    }

    [Fact]
    public async Task Reads_the_entries_before_a_time_Async()
    {
        var page = await ReadPageAsync(new(s_fields) { Time = s_start.AddSeconds(2) });

        Messages(page).Should().Equal("1", "2");
        page.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task Reads_only_entries_matching_the_filter_Async()
    {
        var page = await ReadPageAsync(new(s_fields) { Filter = "_SYSTEMD_UNIT=b.service" });

        Messages(page).Should().Equal("2", "4");
    }

    [Fact]
    public async Task Throws_OperationCanceledException_when_cancelled_while_reading_Async()
    {
        using CancellationTokenSource cancellation = new();
        using var service = new JournalService(() => new FakeSystemdJournal(_entries) { Stepped = cancellation.Cancel });

        var act = () => service.ReadPage(new(s_fields), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Rejects_a_time_before_1970_Async()
    {
        var act = () => ReadPageAsync(new(s_fields) { Time = DateTimeOffset.UnixEpoch.AddDays(-1) });

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Rejects_a_cursor_and_a_time_together_Async()
    {
        var act = () => ReadPageAsync(new(s_fields) { Cursor = _entries[0].Cursor, Time = s_start });

        await act.Should().ThrowAsync<ArgumentException>();
    }
}

[Trait("Category", "Integration")]
public class JournalService_GetPriorityHistogram_SystemJournal
{
    [Fact]
    public async Task Matches_the_priorities_and_units_of_the_full_entries_Async()
    {
        using var service = new JournalService(NullLoggerFactory.Instance, Options.Create(new JournalOptions()));
        var end = DateTimeOffset.UtcNow.AddMinutes(-1);
        var start = end.AddDays(-7);

        var histogram = await service.GetPriorityHistogram(start, end, TimeSpan.FromDays(7), cancellationToken: TestContext.Current.CancellationToken);
        var entries = await service.GetLogs(start, "asc", limit: 1_000_000, cancellationToken: TestContext.Current.CancellationToken);

        var expected = entries
            .Where(e => DateTimeOffset.Parse(e["REALTIME_TIMESTAMP"], CultureInfo.InvariantCulture) < end)
            .Where(e => e.TryGetValue("PRIORITY", out var p) && p is [>= '0' and <= '7'])
            .GroupBy(e => (Group: e.GetValueOrDefault("_SYSTEMD_UNIT") ?? e.GetValueOrDefault("SYSLOG_IDENTIFIER") ?? "", Priority: int.Parse(e["PRIORITY"], CultureInfo.InvariantCulture)))
            .ToDictionary(g => g.Key, g => g.Count());
        var actual = histogram.Groups
            .SelectMany((group, index) => Enumerable.Range(0, JournalHistogram.PriorityLevels)
                .Select(priority => (Key: (Group: group, Priority: priority), Count: histogram.GetCount(0, priority, index))))
            .Where(c => c.Count > 0)
            .ToDictionary(c => c.Key, c => c.Count);

        actual.Should().NotBeEmpty().And.Equal(expected);
    }
}

[Trait("Category", "Integration")]
public class JournalService_ReadPage_SystemJournal
{
    [Fact]
    public async Task Pages_backwards_without_gaps_or_duplicates_Async()
    {
        JournalFieldSet fields = new("MESSAGE");
        using var service = new JournalService(NullLoggerFactory.Instance, Options.Create(new JournalOptions()));
        var time = DateTimeOffset.UtcNow.AddMinutes(-1);
        var whole = await service.ReadPage(new(fields) { Count = 300, Time = time }, TestContext.Current.CancellationToken);

        List<JournalRecord> paged = [];
        JournalPage page = new([], HasMore: true);
        while (paged.Count < whole.Records.Count && page.HasMore)
        {
            var request = paged.Count == 0
                ? new JournalPageRequest(fields) { Count = 50, Time = time }
                : new JournalPageRequest(fields) { Count = 50, Cursor = paged[0].Cursor };
            page = await service.ReadPage(request, TestContext.Current.CancellationToken);
            paged.InsertRange(0, page.Records);
        }

        paged.Select(r => r.Cursor).Should().Equal(whole.Records.Select(r => r.Cursor));
    }
}
