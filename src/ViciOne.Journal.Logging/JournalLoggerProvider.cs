using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ViciOne.Journal;

public sealed class JournalLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentDictionary<string, JournalLogger> _loggers;
    private readonly SystemdJournalWriter _writer;
    private IExternalScopeProvider? _scopeProvider;
    private readonly Action<string> _onError;
    private readonly Channel<IReadOnlyDictionary<string, object?>> _messageQueue = Channel.CreateUnbounded<IReadOnlyDictionary<string, object?>>();
    private readonly Task _outputTask;
    private readonly string _prefix;
    private readonly IReadOnlyDictionary<string, object?> _metaData;
    private readonly bool _writeStructuredData;
    private readonly bool _writeCustomKeyValues;
    private readonly JournalSpamAggregator? _spamAggregator;

    public JournalLoggerProvider(IOptions<JournalLoggerOptions> options)
    {
        _loggers = new();
        _writer = new SystemdJournalWriter(options.Value.OpenMode);
        _outputTask = Task.Factory.StartNew(ProcessLogQueueAsync, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        _onError = options.Value.OnError;
        _prefix = options.Value.EntryPrefix;
        _metaData = options.Value.MetaDataFields;
        _writeStructuredData = options.Value.WriteStructuredData;
        _writeCustomKeyValues = options.Value.WriteCustomKeyValues;
        _spamAggregator = options.Value.SpamGuard.Enabled
            ? new JournalSpamAggregator(options.Value.SpamGuard, ForwardToQueue)
            : null;
    }

    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(categoryName, InitializeLogger);

    private JournalLogger InitializeLogger(string categoryName)
        => new(_prefix, categoryName, _scopeProvider, Log, _metaData.ToDictionary(), _writeCustomKeyValues, _writeStructuredData);

    public void Dispose()
    {
        _spamAggregator?.Dispose();
        _messageQueue.Writer.Complete();
        _outputTask.Wait();
        _outputTask.Dispose();
        _writer.Dispose();
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        => _scopeProvider = scopeProvider;

    private void Log(IReadOnlyDictionary<string, object?> pairList)
    {
        if (_spamAggregator is { } aggregator)
        {
            aggregator.Enqueue(pairList);
            return;
        }

        ForwardToQueue(pairList);
    }

    private void ForwardToQueue(IReadOnlyDictionary<string, object?> entry)
        => _messageQueue.Writer.TryWrite(entry);

    private async Task ProcessLogQueueAsync()
    {
        await foreach (var entry in _messageQueue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                _writer.WriteEntry(entry);
            }
            catch (Exception ex)
            {
                _onError(ex.Message);
            }
        }
    }
}
