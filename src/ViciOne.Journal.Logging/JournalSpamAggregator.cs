using System.Globalization;

namespace ViciOne.Journal;

internal sealed class JournalSpamAggregator : IDisposable
{
    private readonly JournalSpamGuardOptions _options;
    private readonly Action<IReadOnlyDictionary<string, object?>> _forward;
    private readonly Dictionary<SpamKey, SpamBucket> _buckets;
    private readonly Lock _syncRoot = new();
    private readonly string[] _aggregationFields;
    private readonly long _windowTicks;
    private readonly TimeSpan _cleanupInterval;
    private readonly int _threshold;
    private readonly int _maxBucketCapacity;
    private readonly TimeProvider _timeProvider;
    private readonly PeriodicTimer? _timer;
    private readonly CancellationTokenSource? _cts;
    private readonly Task? _cleanupTask;

    private bool _disposed;

    internal JournalSpamAggregator(JournalSpamGuardOptions options, Action<IReadOnlyDictionary<string, object?>> forward)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _forward = forward ?? throw new ArgumentNullException(nameof(forward));
        _aggregationFields = NormalizeFields(options);
        _timeProvider = options.TimeProvider ?? TimeProvider.System;
        _windowTicks = ToTimestampTicks(options.Window, _timeProvider.TimestampFrequency);
        _cleanupInterval = options.FlushInterval > TimeSpan.Zero ? options.FlushInterval : options.Window;
        _threshold = Math.Max(1, options.SummaryCountThreshold);
        _maxBucketCapacity = Math.Max(1, options.MaxBucketCapacity);
        _buckets = new Dictionary<SpamKey, SpamBucket>(_maxBucketCapacity, SpamKeyComparer.Instance);

        if (_windowTicks > 0 && _cleanupInterval > TimeSpan.Zero)
        {
            _cts = new CancellationTokenSource();
            _timer = new PeriodicTimer(_cleanupInterval, _timeProvider);
            _cleanupTask = Task.Run(() => CleanupLoopAsync(_cts.Token));
        }
    }

    public void Enqueue(IReadOnlyDictionary<string, object?> entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (_disposed)
        {
            _forward(entry);
            return;
        }

        var key = SpamKey.Create(entry, _aggregationFields);

        if (key is null)
        {
            _forward(entry);
            return;
        }

        var nowTicks = _timeProvider.GetTimestamp();
        List<SpamSummary>? summaries = null;
        var forwardOriginal = false;

        lock (_syncRoot)
        {
            if (_buckets.TryGetValue(key, out var bucket))
            {
                if (_windowTicks > 0 && bucket.IsWindowExpired(nowTicks, _windowTicks))
                {
                    TryAddSummary(bucket, nowTicks, ref summaries);
                    bucket.Replace(entry, nowTicks);
                    forwardOriginal = true;
                }
                else if (bucket.Record(nowTicks) >= _threshold)
                {
                    TryAddSummary(bucket, nowTicks, ref summaries);
                    _buckets.Remove(key);
                }
            }
            else if (_buckets.Count < _maxBucketCapacity)
            {
                _buckets[key] = new SpamBucket(entry, nowTicks);
                forwardOriginal = true;
            }
            else
            {
                forwardOriginal = true;
            }
        }

        if (forwardOriginal)
            _forward(entry);

        FlushSummaries(summaries);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_cts is not null)
        {
            _cts.Cancel();

            try
            {
                _cleanupTask?.Wait();
            }
            catch (OperationCanceledException)
            {
            }
            catch (AggregateException ex) when (ex.InnerExceptions.Count == 1 && ex.InnerException is OperationCanceledException)
            {
            }
            finally
            {
                _timer?.Dispose();
                _cts.Dispose();
            }
        }

        List<SpamSummary>? summaries;

        lock (_syncRoot)
        {
            summaries = FlushAllBuckets();
        }

        FlushSummaries(summaries);
    }

    private async Task CleanupLoopAsync(CancellationToken token)
    {
        if (_timer is null)
            return;

        try
        {
            while (await _timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                if (_disposed)
                    break;

                List<SpamSummary>? summaries;

                lock (_syncRoot)
                {
                    summaries = CollectExpiredBuckets(_timeProvider.GetTimestamp());
                }

                FlushSummaries(summaries);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private List<SpamSummary>? CollectExpiredBuckets(long nowTicks)
    {
        if (_windowTicks <= 0 || _buckets.Count == 0)
            return null;

        List<SpamSummary>? summaries = null;
        List<SpamKey>? keysToRemove = null;

        foreach (var pair in _buckets)
        {
            if (!pair.Value.ShouldFlush(nowTicks, _windowTicks))
                continue;

            if (pair.Value.TryCreateSummary(nowTicks, out var summary))
                TryAddSummary(summary, ref summaries);

            (keysToRemove ??= []).Add(pair.Key);
        }

        if (keysToRemove is not null)
        {
            foreach (var key in keysToRemove)
                _buckets.Remove(key);
        }

        return summaries;
    }

    private List<SpamSummary>? FlushAllBuckets()
    {
        if (_buckets.Count == 0)
            return null;

        List<SpamSummary>? summaries = null;
        var nowTicks = _timeProvider.GetTimestamp();

        foreach (var bucket in _buckets.Values)
        {
            if (bucket.TryCreateSummary(nowTicks, out var summary))
                TryAddSummary(summary, ref summaries);
        }

        _buckets.Clear();
        return summaries;
    }

    private static void TryAddSummary(SpamBucket bucket, long nowTicks, ref List<SpamSummary>? summaries)
    {
        if (bucket.TryCreateSummary(nowTicks, out var summary))
            TryAddSummary(summary, ref summaries);
    }

    private static void TryAddSummary(SpamSummary summary, ref List<SpamSummary>? summaries)
    {
        summaries ??= [];
        summaries.Add(summary);
    }

    private void FlushSummaries(List<SpamSummary>? summaries)
    {
        if (summaries is null)
            return;

        foreach (var summary in summaries)
            _forward(BuildSummaryEntry(summary));
    }

    private Dictionary<string, object?> BuildSummaryEntry(SpamSummary summary)
    {
        var clone = new Dictionary<string, object?>(summary.Template.Count + 2, StringComparer.Ordinal);

        foreach (var pair in summary.Template)
            clone[pair.Key] = pair.Value;

        var duration = summary.GetDuration(_timeProvider);
        clone["MESSAGE"] = FormatSummary(TryGetMessage(summary.Template), summary.SuppressedCount, duration);
        clone[_options.SummaryCountFieldName] = summary.SuppressedCount;
        clone[_options.SummaryWindowFieldName] = (int)Math.Max(1, Math.Round(duration.TotalMilliseconds, MidpointRounding.AwayFromZero));

        return clone;
    }

    private string FormatSummary(string? originalMessage, int suppressedCount, TimeSpan duration)
    {
        var template = string.IsNullOrWhiteSpace(_options.SummaryMessageTemplate)
            ? "{message} (repeated {count} times in {window})"
            : _options.SummaryMessageTemplate;

        var messageText = originalMessage ?? string.Empty;
        var countText = suppressedCount.ToString(CultureInfo.InvariantCulture);
        var windowText = duration <= TimeSpan.Zero
            ? "0ms"
            : string.Format(CultureInfo.InvariantCulture, "{0:F0}ms", duration.TotalMilliseconds);

        return template
            .Replace("{message}", messageText, StringComparison.Ordinal)
            .Replace("{count}", countText, StringComparison.Ordinal)
            .Replace("{window}", windowText, StringComparison.Ordinal);
    }

    private static string[] NormalizeFields(JournalSpamGuardOptions options)
    {
        if (options.AggregationFields.Count == 0)
            return ["MESSAGE", "CATEGORY", "PRIORITY", "EXCEPTION"];

        var result = new string[options.AggregationFields.Count];
        var index = 0;

        foreach (var field in options.AggregationFields)
        {
            if (string.IsNullOrWhiteSpace(field))
                continue;

            result[index++] = field.ToUpperInvariant();
        }

        if (index == result.Length)
            return result;

        var trimmed = new string[index];
        Array.Copy(result, trimmed, index);
        return trimmed;
    }

    private static long ToTimestampTicks(TimeSpan value, long frequency)
        => value <= TimeSpan.Zero || frequency <= 0
            ? 0
            : (long)Math.Round(value.TotalSeconds * frequency, MidpointRounding.AwayFromZero);

    private static string? TryGetMessage(IReadOnlyDictionary<string, object?> entry)
        => entry.TryGetValue("MESSAGE", out var value)
            ? value?.ToString()
            : null;
}
