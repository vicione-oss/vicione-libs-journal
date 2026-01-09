using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ViciOne.Journal;

namespace JournalLoggerBenchmark;

#pragma warning disable CA1515 // Erwägen Sie, öffentliche Typen intern zu machen.
public class JournalLoggerBenchmark : IDisposable
#pragma warning restore CA1515 // Erwägen Sie, öffentliche Typen intern zu machen.
{
    private JournalLoggerProvider? _provider;
    private ILogger? _logger;
    private bool _disposedValue;

    [Params(false, true)]
    public bool EnableSpamGuard { get; set; }

    [IterationSetup]
    public void Setup()
    {
        if (OperatingSystem.IsLinux())
        {
            var options = new JournalLoggerOptions();
            options.SpamGuard.Enabled = EnableSpamGuard;

            _provider = new JournalLoggerProvider(new OptionsWrapper<JournalLoggerOptions>(options));
            _logger = _provider.CreateLogger("benchmark");
        }
    }

    [Params(10_000)]
    public long Entries { get; set; }

    [Benchmark]
    [SuppressMessage("Naming", "CA1707:Bezeichner dürfen keine Unterstriche enthalten")]
    [SuppressMessage("Performance", "CA1848:LoggerMessage-Delegaten verwenden")]
    public void Journal_Logger_Benchmark()
    {
        for (var i = 0; i < Entries; i++)
            _logger?.LogDebug("benchmark");

        _provider?.Dispose();
        _provider = null;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _provider?.Dispose();
            }
            _disposedValue = true;
        }
    }
}
