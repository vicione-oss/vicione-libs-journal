using System.Collections.Concurrent;
using AwesomeAssertions;
using ViciOne.Journal;

namespace Journal.Tests;

public class JournalQueryPool_Run
{
    private readonly List<FakeJournalEntry> _entries =
    [
        FakeJournalEntry.At(DateTimeOffset.UnixEpoch.AddDays(1), "MESSAGE=a", "_SYSTEMD_UNIT=a.service"),
        FakeJournalEntry.At(DateTimeOffset.UnixEpoch.AddDays(2), "MESSAGE=b", "_SYSTEMD_UNIT=b.service"),
    ];
    private readonly ConcurrentQueue<FakeJournalEntry> _newFileEntries = [];
    private readonly List<FakeSystemdJournal> _opened = [];

    private FakeSystemdJournal Open()
    {
        var journal = new FakeSystemdJournal(_entries, _newFileEntries);
        _opened.Add(journal);
        return journal;
    }

    private static int CountEntries(ISystemdJournal journal)
    {
        journal.SeekHead();
        var count = 0;
        while (journal.Next())
            count++;
        return count;
    }

    [Fact]
    public async Task Reuses_the_open_journal_for_the_next_query_Async()
    {
        using JournalQueryPool pool = new(Open, size: 2, logger: null);

        await pool.Run(CountEntries, TestContext.Current.CancellationToken);
        await pool.Run(CountEntries, TestContext.Current.CancellationToken);

        _opened.Should().ContainSingle();
    }

    [Fact]
    public async Task Starts_each_query_without_the_matches_of_the_previous_one_Async()
    {
        using JournalQueryPool pool = new(Open, size: 1, logger: null);
        await pool.Run(journal =>
        {
            journal.AddMatch("_SYSTEMD_UNIT=a.service");
            return CountEntries(journal);
        }, TestContext.Current.CancellationToken);

        var count = await pool.Run(CountEntries, TestContext.Current.CancellationToken);

        count.Should().Be(2);
    }

    [Fact]
    public async Task Sees_entries_of_journal_files_created_after_it_opened_Async()
    {
        using JournalQueryPool pool = new(Open, size: 1, logger: null);
        await pool.Run(CountEntries, TestContext.Current.CancellationToken);
        _newFileEntries.Enqueue(FakeJournalEntry.At(DateTimeOffset.UnixEpoch.AddDays(3), "MESSAGE=c"));

        var count = await pool.Run(CountEntries, TestContext.Current.CancellationToken);

        count.Should().Be(3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Replaces_the_journal_after_a_native_error_Async(bool wrapped)
    {
        using JournalQueryPool pool = new(Open, size: 1, logger: null);
        JournalException native = new("failed", -5);
        Exception error = wrapped ? new InvalidOperationException("Error reading journal entries.", native) : native;
        var failing = () => pool.Run<int>(_ => throw error, TestContext.Current.CancellationToken);
        await failing.Should().ThrowAsync<Exception>();

        await pool.Run(CountEntries, TestContext.Current.CancellationToken);

        _opened.Should().HaveCount(2);
        _opened[0].IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task Keeps_the_journal_after_invalid_input_Async()
    {
        using JournalQueryPool pool = new(Open, size: 1, logger: null);
        var failing = () => pool.Run<int>(_ => throw new InvalidOperationException("Error applying filters.", new ArgumentException("bad")), TestContext.Current.CancellationToken);
        await failing.Should().ThrowAsync<InvalidOperationException>();

        await pool.Run(CountEntries, TestContext.Current.CancellationToken);

        _opened.Should().ContainSingle().Which.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public async Task Keeps_the_journal_of_a_cancelled_query_Async()
    {
        using JournalQueryPool pool = new(Open, size: 1, logger: null);
        var cancelled = () => pool.Run<int>(_ => throw new OperationCanceledException(), TestContext.Current.CancellationToken);
        await cancelled.Should().ThrowAsync<OperationCanceledException>();

        await pool.Run(CountEntries, TestContext.Current.CancellationToken);

        _opened.Should().ContainSingle().Which.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public async Task Does_not_start_a_query_with_a_cancelled_token_Async()
    {
        using JournalQueryPool pool = new(Open, size: 1, logger: null);
        await pool.Run(CountEntries, TestContext.Current.CancellationToken);
        var started = false;

        var act = () => pool.Run(_ => started = true, new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
        started.Should().BeFalse();
    }

    [Fact]
    public async Task Throws_a_busy_exception_when_every_journal_runs_a_query_Async()
    {
        using JournalQueryPool pool = new(Open, size: 1, logger: null);
        using SemaphoreSlim release = new(0);
        var running = pool.Run(_ =>
        {
            release.Wait(TestContext.Current.CancellationToken);
            return 0;
        }, TestContext.Current.CancellationToken);

        var act = () => pool.Run(CountEntries, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<JournalBusyException>();
        release.Release();
        await running;
    }
}

public class JournalQueryPool_Dispose
{
    [Fact]
    public async Task Closes_the_idle_journals_Async()
    {
        List<FakeSystemdJournal> opened = [];
        var pool = new JournalQueryPool(() =>
        {
            var journal = new FakeSystemdJournal([]);
            opened.Add(journal);
            return journal;
        }, size: 1, logger: null);
        await pool.Run(_ => 0, TestContext.Current.CancellationToken);

        pool.Dispose();

        opened.Should().ContainSingle().Which.IsDisposed.Should().BeTrue();
    }
}
