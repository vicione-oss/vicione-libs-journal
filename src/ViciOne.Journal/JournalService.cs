using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ViciOne.Journal;

public sealed class JournalService : IJournalService
{
    private readonly ILogger<SystemdJournal>? _systemdLogger;
    private readonly Func<ISystemdJournal> _openJournal;
    private readonly AccessControl _accessControl = new(5);

    public JournalService(ILoggerFactory loggerFactory, IOptions<JournalOptions> options)
    {
        var logger = options.Value.EnableLogging ? loggerFactory.CreateLogger<SystemdJournal>() : null;
        var openingMode = options.Value.OpeningMode;

        _systemdLogger = logger;
        _openJournal = () => new SystemdJournal(logger, openingMode);
    }

    internal JournalService(Func<ISystemdJournal> openJournal)
        => _openJournal = openJournal;

    [SuppressMessage("Performance", "CA1822:Member als statisch markieren")]
    public IReadOnlyDictionary<string, string> GetFieldDescriptions()
        => JournalFieldDescriptions.FieldDescriptions;

    public async Task<IReadOnlyCollection<string>> GetFields(CancellationToken cancellationToken = default)
        => await _accessControl.Run(() =>
        {
            using var journal = _openJournal();
            return journal.EnumerateAvailableFields();
        }, cancellationToken);

    public async Task<IReadOnlyCollection<string>> GetFieldValues(string field, CancellationToken cancellationToken = default)
        => await _accessControl.Run(() =>
        {
            using var journal = _openJournal();
            return journal.EnumerateUniqueFieldValues(field);
        }, cancellationToken);

    /// <param name="order">
    /// <c>asc</c> or <c>desc</c>, in any case. Either way, the result holds the first <paramref name="limit"/>
    /// entries from <paramref name="since"/>; <c>desc</c> returns them newest first.
    /// </param>
    public async Task<IReadOnlyCollection<IReadOnlyDictionary<string, string>>> GetLogs(DateTimeOffset since, string order = "desc", int limit = 100, string? filter = null, CancellationToken cancellationToken = default)
        => await _accessControl.Run(() =>
        {
            if (limit < 0)
                throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be a positiv value.");
            var descending = order.Equals("desc", StringComparison.OrdinalIgnoreCase);
            if (!descending && !order.Equals("asc", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Order must be 'asc' or 'desc'.", nameof(order));
            if (since > DateTimeOffset.Now)
                throw new ArgumentOutOfRangeException(nameof(since), "Since must be in the past.");

            using var journal = _openJournal();

            try
            {
                journal.SeekRealtime(since);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error setting journal pointer to {since}.", ex);
            }

            try
            {
                if (!string.IsNullOrEmpty(filter))
                    Filter.Apply(filter, journal);
                //JournalFilterService.ApplyFilters(journal, filter, _systemdLogger);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error applying filters. ({filter})", ex);
            }

            try
            {
                List<IReadOnlyDictionary<string, string>> logs = new(limit);

                while (logs.Count < limit && journal.Next())
                    logs.Add(journal.GetCurrentEntry());

                if (descending)
                    logs.Reverse();

                return logs;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Error reading journal entries.", ex);
            }
        }, cancellationToken);

    // TODO: proof if needed
    internal IReadOnlyCollection<IReadOnlyDictionary<string, string>> GetLogs(DateTimeOffset since, int pageSize, int pageNumber, string? filter = null)
    {
        if (since > DateTimeOffset.Now)
            throw new ArgumentOutOfRangeException(nameof(since), "Since must be in the past.");
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be greater than 0.");
        if (pageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than 0.");

        using var journal = _openJournal();

        try
        {
            journal.SeekRealtime(since);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error setting journal pointer to {since}.", ex);
        }

        try
        {
            if (!string.IsNullOrEmpty(filter))
                Filter.Apply(filter, journal);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error applying filters. ({filter})", ex);
        }

        try
        {
            List<IReadOnlyDictionary<string, string>> logs = new(pageSize);
            var skip = (pageNumber - 1) * pageSize;

            while (journal.Next())
            {
                if (skip > 0)
                {
                    skip--;
                    continue;
                }

                var entry = journal.GetCurrentEntry();
                logs.Insert(0, entry);
            }

            return logs;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error reading journal entries.", ex);
        }
    }

    public async Task<IReadOnlyDictionary<int, int>> GetPriorityCounts(DateTimeOffset since, string? filter = null, CancellationToken cancellationToken = default)
        => await _accessControl.Run(() =>
        {
            using var journal = _openJournal();

            try
            {
                if (!string.IsNullOrEmpty(filter))
                    Filter.Apply(filter, journal);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error applying filters. ({filter})", ex);
            }

            try
            {
                return journal.GetPriorityCountsSince(since.UtcDateTime, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error reading priority counts.", ex);
            }
        }, cancellationToken);

    public async Task<IReadOnlyDictionary<int, int>> GetPriorityCounts(DateTimeOffset start, DateTimeOffset end, string? filter = null, CancellationToken cancellationToken = default)
        => await _accessControl.Run(() =>
        {
            using var journal = _openJournal();

            try
            {
                if (!string.IsNullOrEmpty(filter))
                    Filter.Apply(filter, journal);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error applying filters. ({filter})", ex);
            }

            try
            {
                return journal.GetPriorityCountsBetween(start.UtcDateTime, end.UtcDateTime, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error reading priority counts.", ex);
            }
        }, cancellationToken);

    public Task<JournalHistogram> GetPriorityHistogram(DateTimeOffset start, DateTimeOffset end, TimeSpan bucketSize, string? filter = null, string? groupBy = JournalHistogram.DefaultGroupBy, CancellationToken cancellationToken = default)
        => GetPriorityHistogram(PriorityHistogramCounter.CreateBucketEdges(start, end, bucketSize), filter, groupBy, cancellationToken);

    public async Task<JournalHistogram> GetPriorityHistogram(IReadOnlyList<DateTimeOffset> bucketEdges, string? filter = null, string? groupBy = JournalHistogram.DefaultGroupBy, CancellationToken cancellationToken = default)
    {
        PriorityHistogramCounter.Validate(bucketEdges, groupBy);

        return await _accessControl.Run(() =>
        {
            using var journal = _openJournal();

            try
            {
                if (!string.IsNullOrEmpty(filter))
                    Filter.Apply(filter, journal);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error applying filters. ({filter})", ex);
            }

            try
            {
                return PriorityHistogramCounter.Count(journal, bucketEdges, groupBy, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException("Error reading priority counts.", ex);
            }
        }, cancellationToken);
    }

    [SuppressMessage("Reliability", "CA2000:Objekte verwerfen, bevor Bereich verloren geht")]
    public async Task<IJournalGenericSession> CreateGeneric(CancellationToken cancellationToken = default)
    {
        var access = await _accessControl.Begin(cancellationToken);
        return new JournalGenericSession(_openJournal, access.Dispose, _systemdLogger);
    }

    [SuppressMessage("Reliability", "CA2000:Objekte verwerfen, bevor Bereich verloren geht")]
    public async Task<IJournalFollowSession> CreateFollower(TimeSpan interval, CancellationToken cancellation = default)
    {
        var access = await _accessControl.Begin(cancellation);
        return new JournalFollowSession(_openJournal, interval, access.Dispose, _systemdLogger);
    }

    public void Dispose()
        => _accessControl.Dispose();
}
