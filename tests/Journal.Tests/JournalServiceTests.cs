using AwesomeAssertions;
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
