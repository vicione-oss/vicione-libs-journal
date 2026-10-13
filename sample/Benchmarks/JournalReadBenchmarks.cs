using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using ViciOne.Journal;

namespace Benchmarks;

/// <summary>
/// Reads the journal files of <c>JOURNAL_DIRECTORY</c>, such as a copy from an edge device, or the local journal
/// without it. With a directory, the range ends at its newest entry.
/// </summary>
[SuppressMessage("Maintainability", "CA1515:Erwägen Sie, öffentliche Typen intern zu machen.")]
public abstract class JournalDirectorySetup : JournalBenchmarkSetup
{
    private static readonly string? s_directory = Environment.GetEnvironmentVariable("JOURNAL_DIRECTORY");

    protected override JournalOptions CreateOptions()
    {
        var options = base.CreateOptions();
        options.Directory = s_directory;
        return options;
    }

    protected override DateTimeOffset GetRangeEnd()
        => s_directory is null
            ? base.GetRangeEnd()
            : (Service.GetTimeRange().GetAwaiter().GetResult()?.Newest ?? throw new InvalidOperationException("The journal is empty.")).AddTicks(10);
}

[MemoryDiagnoser]
[SuppressMessage("Maintainability", "CA1515:Erwägen Sie, öffentliche Typen intern zu machen.")]
public class JournalChartBenchmark : JournalDirectorySetup
{
    [Benchmark(Baseline = true)]
    public async Task<int> CountsPerBucket()
    {
        var bucketSize = (End - Start) / Buckets;
        var total = 0;
        for (var i = 0; i < Buckets; i++)
        {
            var counts = await Service.GetPriorityCounts(Start + (bucketSize * i), Start + (bucketSize * (i + 1)));
            total += counts.Values.Sum();
        }

        return total;
    }

    [Benchmark]
    public async Task<int> Histogram()
        => (await Service.GetPriorityHistogram(Start, End, (End - Start) / Buckets, groupBy: null)).BucketCount;

    [Benchmark]
    public async Task<int> HistogramByUnit()
        => (await Service.GetPriorityHistogram(Start, End, (End - Start) / Buckets)).Groups.Count;
}

[MemoryDiagnoser]
[SuppressMessage("Maintainability", "CA1515:Erwägen Sie, öffentliche Typen intern zu machen.")]
public class JournalListBenchmark : JournalDirectorySetup
{
    private static readonly JournalFieldSet s_listFields = new(
        "MESSAGE", "_SYSTEMD_UNIT", "SYSLOG_IDENTIFIER", "_PID", "_HOSTNAME", "SOURCE_CONTEXT", "MODULE_ID");

    [Params(100, 1000)]
    public int PageSize { get; set; }

    [Benchmark(Baseline = true)]
    public async Task<int> FullEntries()
        => (await Service.GetLogs(Start, "asc", PageSize)).Count;

    [Benchmark]
    public async Task<int> ProjectedRecords()
        => (await Service.ReadPage(new(s_listFields) { Count = PageSize, Direction = JournalDirection.Newer, Time = Start })).Records.Count;
}
