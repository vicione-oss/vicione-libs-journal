
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using BenchmarkDotNet.Attributes;
using Serilog;
using Serilog.Sinks.Journal;

namespace Benchmarks;

[MemoryDiagnoser]
[SuppressMessage("Maintainability", "CA1515:Erwägen Sie, öffentliche Typen intern zu machen.")]
public class SerilogSinkBenchmark
{
    [IterationSetup]
    public static void Setup()
        => Log.Logger = new LoggerConfiguration()
            .WriteTo.Journal(CultureInfo.InvariantCulture)
            .CreateLogger();

    [Params(1_000_000)]
    public long Entries { get; set; }

    [Benchmark]
    [SuppressMessage("Naming", "CA1707:Bezeichner dürfen keine Unterstriche enthalten")]
    public void Log_Message_to_Journal()
    {
        for (var i = 0; i < Entries; i++)
            Log.Debug("Bechnmark");

        Log.CloseAndFlush();
    }
}
