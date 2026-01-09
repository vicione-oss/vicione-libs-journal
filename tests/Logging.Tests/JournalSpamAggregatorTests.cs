using FluentAssertions;
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
        var forwarded = new List<IReadOnlyDictionary<string, object?>>();
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

        await AdvanceAsync(time, TimeSpan.FromMilliseconds(150));

        forwarded.Should().HaveCount(2);
        forwarded[^1].Should().ContainKey("SPAM_COUNT").WhoseValue.Should().Be(1);
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
        forwarded.Last().Should().ContainKey("SPAM_COUNT").WhoseValue.Should().Be(1);
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

        CountWhere(forwarded, entry => entry.ContainsKey(options.SummaryCountFieldName)).Should().Be(1);
        CountWhere(forwarded, entry => HasMessage(entry, "No capacity")).Should().Be(2);
    }

    [Fact]
    public async Task Forwards_new_entry_after_window_flush()
    {
        var forwarded = new List<IReadOnlyDictionary<string, object?>>();
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

        await AdvanceAsync(time, TimeSpan.FromMilliseconds(150));

        forwarded.Should().HaveCount(2);
        forwarded[1].Should().ContainKey("SPAM_COUNT").WhoseValue.Should().Be(1);

        aggregator.Enqueue(CreateEntry("SERVICE", "Flushed"));

        forwarded.Should().HaveCount(3);
        forwarded[^1].Should().Contain(new KeyValuePair<string, object?>("MESSAGE", "Flushed"));
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

    private static async Task AdvanceAsync(FakeTimeProvider provider, TimeSpan duration)
    {
        provider.Advance(duration);
        await Task.Yield();
    }
}
