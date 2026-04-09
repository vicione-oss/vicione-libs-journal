using System.Collections.Concurrent;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using ViciOne.Journal;

namespace Logging.Tests;

public sealed class JournalSpamAggregatorTests
{
    [Fact]
    public void Emits_summary_after_threshold()
    {
        var forwarded = new List<IReadOnlyDictionary<string, object?>>();
        var time = new FakeTimeProvider();
        var options = new JournalSpamGuardOptions
        {
            Enabled = true,
            SummaryCountThreshold = 2,
            Window = TimeSpan.FromSeconds(10),
            FlushInterval = TimeSpan.FromSeconds(10),
            TimeProvider = time,
        };

        using var aggregator = new JournalSpamAggregator(options, forwarded.Add);

        aggregator.Enqueue(CreateEntry("SERVICE", "Connection lost"));
        aggregator.Enqueue(CreateEntry("SERVICE", "Connection lost"));
        aggregator.Enqueue(CreateEntry("SERVICE", "Connection lost"));

        forwarded.Should().HaveCount(2);
        forwarded[1].Should().ContainKey("SPAM_COUNT").WhoseValue.Should().Be(2);
        forwarded[1].Should().ContainKey("MESSAGE").WhoseValue.Should().BeOfType<string>().Which.Should().Contain("repeated 2 times");
    }

    [Fact]
    public async Task Flushes_summary_after_window_elapsed()
    {
        var forwarded = new ConcurrentBag<IReadOnlyDictionary<string, object?>>();
        var time = new FakeTimeProvider();
        var options = new JournalSpamGuardOptions
        {
            Enabled = true,
            SummaryCountThreshold = 50,
            Window = TimeSpan.FromMilliseconds(50),
            FlushInterval = TimeSpan.FromMilliseconds(10),
            TimeProvider = time,
        };

        using var aggregator = new JournalSpamAggregator(options, forwarded.Add);

        aggregator.Enqueue(CreateEntry("SERVICE", "Slow repeat"));
        aggregator.Enqueue(CreateEntry("SERVICE", "Slow repeat"));

        time.Advance(TimeSpan.FromMilliseconds(150));
        await WaitForCountAsync(forwarded, 2);

        var snapshot = forwarded.ToList();
        snapshot.Should().HaveCount(2);
        snapshot.Should().AllSatisfy(e => e.Should().ContainKey("MESSAGE").WhoseValue.Should().Be("Slow repeat"));
        snapshot.Should().AllSatisfy(e => e.Should().NotContainKey("SPAM_COUNT"));
    }

    [Fact]
    public void Dispose_flushes_pending_counts()
    {
        var forwarded = new List<IReadOnlyDictionary<string, object?>>();
        var time = new FakeTimeProvider();
        var options = new JournalSpamGuardOptions
        {
            Enabled = true,
            SummaryCountThreshold = 10,
            Window = TimeSpan.FromSeconds(30),
            FlushInterval = TimeSpan.Zero,
            TimeProvider = time,
        };

        var aggregator = new JournalSpamAggregator(options, forwarded.Add);

        aggregator.Enqueue(CreateEntry("SERVICE", "Dispose repeat"));
        aggregator.Enqueue(CreateEntry("SERVICE", "Dispose repeat"));

        forwarded.Should().HaveCount(1);

        aggregator.Dispose();

        forwarded.Should().HaveCount(2);
        forwarded.Last().Should().ContainKey("MESSAGE").WhoseValue.Should().Be("Dispose repeat");
        forwarded.Last().Should().NotContainKey("SPAM_COUNT");
    }

    [Fact]
    public void Respects_bucket_capacity()
    {
        var forwarded = new List<IReadOnlyDictionary<string, object?>>();
        var time = new FakeTimeProvider();
        var options = new JournalSpamGuardOptions
        {
            Enabled = true,
            SummaryCountThreshold = 5,
            Window = TimeSpan.FromSeconds(5),
            FlushInterval = TimeSpan.FromSeconds(5),
            MaxBucketCapacity = 1,
            TimeProvider = time,
        };

        using var aggregator = new JournalSpamAggregator(options, forwarded.Add);

        aggregator.Enqueue(CreateEntry("SERVICE", "Limited"));
        aggregator.Enqueue(CreateEntry("SERVICE", "Limited"));
        aggregator.Enqueue(CreateEntry("OTHER", "No capacity"));
        aggregator.Enqueue(CreateEntry("OTHER", "No capacity"));

        aggregator.Dispose();

        CountWhere(forwarded, entry => entry.ContainsKey(options.SummaryCountFieldName)).Should().Be(0);
        CountWhere(forwarded, entry => HasMessage(entry, "No capacity")).Should().Be(2);
    }

    [Fact]
    public async Task Forwards_new_entry_after_window_flush()
    {
        var forwarded = new ConcurrentBag<IReadOnlyDictionary<string, object?>>();
        var time = new FakeTimeProvider();
        var options = new JournalSpamGuardOptions
        {
            Enabled = true,
            SummaryCountThreshold = 100,
            Window = TimeSpan.FromMilliseconds(50),
            FlushInterval = TimeSpan.FromMilliseconds(10),
            TimeProvider = time,
        };

        using var aggregator = new JournalSpamAggregator(options, forwarded.Add);

        aggregator.Enqueue(CreateEntry("SERVICE", "Flushed"));
        aggregator.Enqueue(CreateEntry("SERVICE", "Flushed"));

        forwarded.Should().HaveCount(1);

        time.Advance(TimeSpan.FromMilliseconds(150));
        await WaitForCountAsync(forwarded, 2);

        forwarded.Should().HaveCount(2);
        forwarded.ToList().Should().AllSatisfy(e => e.Should().NotContainKey("SPAM_COUNT"));

        aggregator.Enqueue(CreateEntry("SERVICE", "Flushed"));

        forwarded.Should().HaveCount(3);
        forwarded.First(e => HasMessage(e, "Flushed")).Should().Contain(new KeyValuePair<string, object?>("MESSAGE", "Flushed"));
    }

    [Fact]
    public void Aggregates_using_custom_fields()
    {
        var forwarded = new List<IReadOnlyDictionary<string, object?>>();
        var time = new FakeTimeProvider();
        var options = new JournalSpamGuardOptions
        {
            Enabled = true,
            SummaryCountThreshold = 2,
            Window = TimeSpan.FromSeconds(10),
            FlushInterval = TimeSpan.FromSeconds(10),
            TimeProvider = time,
        };

        options.AggregationFields.Clear();
        options.AggregationFields.Add("CATEGORY");

        using var aggregator = new JournalSpamAggregator(options, forwarded.Add);

        aggregator.Enqueue(CreateEntry("SERVICE", "Variant A"));
        aggregator.Enqueue(CreateEntry("SERVICE", "Variant B"));
        aggregator.Enqueue(CreateEntry("SERVICE", "Variant C"));

        forwarded.Should().HaveCount(2);
        forwarded[1].Should().ContainKey("SPAM_COUNT").WhoseValue.Should().Be(2);
        forwarded[1].Should().ContainKey("MESSAGE").WhoseValue.Should().BeOfType<string>().Which.Should().Contain("repeated 2 times");
    }

    [Fact]
    public async Task Uses_fixed_window()
    {
        // Fixed window: expires based on first entry timestamp.
        // NOT a moving window that resets with each duplicate.
        var forwarded = new ConcurrentBag<IReadOnlyDictionary<string, object?>>();
        var time = new FakeTimeProvider();
        var options = new JournalSpamGuardOptions
        {
            Enabled = true,
            SummaryCountThreshold = 100,
            Window = TimeSpan.FromMilliseconds(100),
            FlushInterval = TimeSpan.FromMilliseconds(25),
            TimeProvider = time,
        };

        using var aggregator = new JournalSpamAggregator(options, forwarded.Add);

        aggregator.Enqueue(CreateEntry("SERVICE", "Continuous spam"));  // t=0
        time.Advance(TimeSpan.FromMilliseconds(30));
        aggregator.Enqueue(CreateEntry("SERVICE", "Continuous spam"));  // t=30
        time.Advance(TimeSpan.FromMilliseconds(30));
        aggregator.Enqueue(CreateEntry("SERVICE", "Continuous spam"));  // t=60
        time.Advance(TimeSpan.FromMilliseconds(30));
        aggregator.Enqueue(CreateEntry("SERVICE", "Continuous spam"));  // t=90

        // Window expires at t=100 (from first entry), not t=190 (from last).
        time.Advance(TimeSpan.FromMilliseconds(50));  // t=140
        await WaitForCountAsync(forwarded, 2);

        var snapshot = forwarded.ToList();
        snapshot.Should().HaveCount(2);
        snapshot.First(e => e.ContainsKey("SPAM_COUNT")).Should().ContainKey("SPAM_COUNT").WhoseValue.Should().Be(3);
    }

    private static Dictionary<string, object?> CreateEntry(string category, string message)
    {
        var items = new[]
        {
            new KeyValuePair<string, object?>("CATEGORY", category),
        };

        return JournalEntryFactory.CreateLogEntry(items, message, LogLevel.Error, false, false);
    }

    private static bool HasMessage(IReadOnlyDictionary<string, object?> entry, string expected)
        => entry.TryGetValue("MESSAGE", out var value)
           && value is string text
           && string.Equals(text, expected, StringComparison.Ordinal);

    private static int CountWhere(IEnumerable<IReadOnlyDictionary<string, object?>> source, Func<IReadOnlyDictionary<string, object?>, bool> predicate)
    {
        var count = 0;

        foreach (var entry in source)
        {
            if (predicate(entry))
                count++;
        }

        return count;
    }

    private static async Task WaitForCountAsync<T>(ConcurrentBag<T> collection, int expectedCount, int timeoutMs = 1000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (collection.Count < expectedCount && Environment.TickCount64 < deadline)
        {
            await Task.Delay(10);
        }
    }
}
