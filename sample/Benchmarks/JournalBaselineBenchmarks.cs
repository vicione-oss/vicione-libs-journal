using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ViciOne.Journal;

namespace Benchmarks;

/// <summary>
/// Reads the local journal over the last <c>JOURNAL_DAYS</c> days (default 7). Runs on Linux only.
/// <c>JOURNAL_OPENING_MODE=None</c> also reads the journals other machines sent, which needs root.
/// </summary>
/// <remarks>
/// This file uses only the API of <c>1.2.0</c>, so the same benchmarks run before and after an upgrade.
/// </remarks>
[SuppressMessage("Maintainability", "CA1515:Erwägen Sie, öffentliche Typen intern zu machen.")]
public abstract class JournalBenchmarkSetup
{
    protected const int Buckets = 60;

    protected JournalService Service { get; private set; } = null!;
    protected DateTimeOffset Start { get; private set; }
    protected DateTimeOffset End { get; private set; }

    [GlobalSetup]
    public void OpenJournal()
    {
        Service = new(NullLoggerFactory.Instance, Options.Create(CreateOptions()));

        var days = int.TryParse(Environment.GetEnvironmentVariable("JOURNAL_DAYS"), out var value) ? value : 7;
        End = GetRangeEnd();
        Start = End.AddDays(-days);
    }

    [GlobalCleanup]
    public void CloseJournal()
        => Service.Dispose();

    protected virtual JournalOptions CreateOptions()
    {
        var options = new JournalOptions();
        options.OpeningMode = ParseOrDefault(Environment.GetEnvironmentVariable("JOURNAL_OPENING_MODE"), options.OpeningMode);
        return options;
    }

    protected virtual DateTimeOffset GetRangeEnd()
        => DateTimeOffset.Now;

    // The logging packages define a JournalOpeningMode of their own in the same namespace, so the type is inferred, not named.
    private static T ParseOrDefault<T>(string? value, T fallback)
        where T : struct, Enum
        => Enum.TryParse<T>(value, out var parsed) ? parsed : fallback;
}

/// <summary>
/// Draws a chart with <c>1.2.0</c>, which has no working count per interval: each bar counts from its start to now.
/// </summary>
[MemoryDiagnoser]
[SuppressMessage("Maintainability", "CA1515:Erwägen Sie, öffentliche Typen intern zu machen.")]
public class JournalChartBaseline : JournalBenchmarkSetup
{
    [Benchmark]
    public async Task<int> CountsSinceEachBucket()
    {
        var bucketSize = (End - Start) / Buckets;
        var total = 0;
        for (var i = 0; i < Buckets; i++)
        {
            var counts = await Service.GetPriorityCounts(Start + (bucketSize * i));
            total += counts.Values.Sum();
        }

        return total;
    }
}

/// <summary>
/// Reads a page of the list with every field of each entry, as <c>1.2.0</c> does.
/// </summary>
[MemoryDiagnoser]
[SuppressMessage("Maintainability", "CA1515:Erwägen Sie, öffentliche Typen intern zu machen.")]
public class JournalListBaseline : JournalBenchmarkSetup
{
    [Params(100, 1000)]
    public int PageSize { get; set; }

    [Benchmark]
    public async Task<int> FullEntries()
        => (await Service.GetLogs(Start, "asc", PageSize)).Count;
}
