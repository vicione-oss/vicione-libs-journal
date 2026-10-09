using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ViciOne.Journal;

public sealed class JournalService : IJournalService
{
    private readonly ILogger<SystemdJournal>? _systemdLogger;
    private readonly Func<ISystemdJournal> _openJournal;
    private readonly AccessControl _sessions;
    private readonly JournalQueryPool _queries;

    public JournalService(ILoggerFactory loggerFactory, IOptions<JournalOptions> options)
        : this(options.Value, options.Value.EnableLogging ? loggerFactory.CreateLogger<SystemdJournal>() : null)
    { }

    private JournalService(JournalOptions options, ILogger<SystemdJournal>? logger)
        : this(() => new SystemdJournal(logger, options.OpeningMode), options, logger)
    { }

    internal JournalService(Func<ISystemdJournal> openJournal, JournalOptions? options = null, ILogger<SystemdJournal>? logger = null)
    {
        options ??= new();
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxSessions, 1, nameof(JournalOptions.MaxSessions));
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxConcurrentQueries, 1, nameof(JournalOptions.MaxConcurrentQueries));
        _openJournal = openJournal;
        _systemdLogger = logger;
        _sessions = new(options.MaxSessions, $"Only {options.MaxSessions} journal sessions may be open at once.");
        _queries = new(openJournal, options.MaxConcurrentQueries, logger);
    }

    [SuppressMessage("Performance", "CA1822:Member als statisch markieren")]
    public IReadOnlyDictionary<string, string> GetFieldDescriptions()
        => JournalFieldDescriptions.FieldDescriptions;

    public Task<IReadOnlyCollection<string>> GetFields(CancellationToken cancellationToken = default)
        => RunQuery<IReadOnlyCollection<string>>(journal => journal.EnumerateAvailableFields(), cancellationToken);

    public Task<IReadOnlyCollection<string>> GetFieldValues(string field, CancellationToken cancellationToken = default)
        => RunQuery<IReadOnlyCollection<string>>(journal => journal.EnumerateUniqueFieldValues(field), cancellationToken);

    /// <param name="order">
    /// <c>asc</c> or <c>desc</c>, in any case. Either way, the result holds the first <paramref name="limit"/>
    /// entries from <paramref name="since"/>; <c>desc</c> returns them newest first.
    /// </param>
    public async Task<IReadOnlyCollection<IReadOnlyDictionary<string, string>>> GetLogs(DateTimeOffset since, string order = "desc", int limit = 100, string? filter = null, CancellationToken cancellationToken = default)
    {
        if (limit < 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be a positiv value.");
        var descending = order.Equals("desc", StringComparison.OrdinalIgnoreCase);
        if (!descending && !order.Equals("asc", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Order must be 'asc' or 'desc'.", nameof(order));
        if (since > DateTimeOffset.Now)
            throw new ArgumentOutOfRangeException(nameof(since), "Since must be in the past.");

        return await RunQuery<IReadOnlyCollection<IReadOnlyDictionary<string, string>>>(journal =>
        {
            try
            {
                journal.SeekRealtime(since);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error setting journal pointer to {since}.", ex);
            }

            ApplyFilter(journal, filter);

            try
            {
                List<IReadOnlyDictionary<string, string>> logs = new(limit);

                while (logs.Count < limit && journal.Next())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    logs.Add(journal.GetCurrentEntry());
                }

                if (descending)
                    logs.Reverse();

                return logs;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException("Error reading journal entries.", ex);
            }
        }, cancellationToken);
    }

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

        ApplyFilter(journal, filter);

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
    {
        if (since > DateTimeOffset.Now)
            throw new ArgumentOutOfRangeException(nameof(since), "Since must be in the past.");

        return await GetPriorityCounts(since, DateTimeOffset.Now, filter, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, int>> GetPriorityCounts(DateTimeOffset start, DateTimeOffset end, string? filter = null, CancellationToken cancellationToken = default)
    {
        var histogram = await GetPriorityHistogram([start, end], filter, groupBy: null, cancellationToken);

        return Enumerable.Range(0, JournalHistogram.PriorityLevels).ToDictionary(p => p, p => histogram.GetCount(0, p));
    }

    public Task<JournalHistogram> GetPriorityHistogram(DateTimeOffset start, DateTimeOffset end, TimeSpan bucketSize, string? filter = null, string? groupBy = JournalHistogram.DefaultGroupBy, CancellationToken cancellationToken = default)
        => GetPriorityHistogram(PriorityHistogramCounter.CreateBucketEdges(start, end, bucketSize), filter, groupBy, cancellationToken);

    public async Task<JournalHistogram> GetPriorityHistogram(IReadOnlyList<DateTimeOffset> bucketEdges, string? filter = null, string? groupBy = JournalHistogram.DefaultGroupBy, CancellationToken cancellationToken = default)
    {
        PriorityHistogramCounter.Validate(bucketEdges, groupBy);

        return await RunQuery(journal =>
        {
            ApplyFilter(journal, filter);

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

    /// <exception cref="JournalBusyException">Thrown when <see cref="JournalOptions.MaxSessions"/> sessions are open.</exception>
    public Task<IJournalGenericSession> CreateGeneric(CancellationToken cancellationToken = default)
        => OpenSession<IJournalGenericSession>(release => new JournalGenericSession(_openJournal, release, _systemdLogger), cancellationToken);

    /// <exception cref="JournalBusyException">Thrown when <see cref="JournalOptions.MaxSessions"/> sessions are open.</exception>
    public Task<IJournalFollowSession> CreateFollower(TimeSpan interval, CancellationToken cancellation = default)
        => OpenSession<IJournalFollowSession>(release => new JournalFollowSession(_openJournal, interval, release, _systemdLogger), cancellation);

    public void Dispose()
    {
        _sessions.Dispose();
        _queries.Dispose();
    }

    private static void ApplyFilter(ISystemdJournal journal, string? filter)
    {
        try
        {
            if (!string.IsNullOrEmpty(filter))
                Filter.Apply(filter, journal);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error applying filters. ({filter})", ex);
        }
    }

    private Task<T> RunQuery<T>(Func<ISystemdJournal, T> query, CancellationToken cancellationToken)
        => _queries.Run(query, cancellationToken);

    [SuppressMessage("Reliability", "CA2000:Objekte verwerfen, bevor Bereich verloren geht", Justification = "The session releases the slot when it is disposed.")]
    private Task<T> OpenSession<T>(Func<Action, T> create, CancellationToken cancellationToken)
        => Task.Run(() =>
        {
            var slot = _sessions.Acquire();
            try
            {
                return create(slot.Dispose);
            }
            catch
            {
                slot.Dispose();
                throw;
            }
        }, cancellationToken);
}
