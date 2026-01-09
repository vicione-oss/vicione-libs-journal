using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ViciOne.Journal;

public sealed class JournalService(ILoggerFactory loggerFactory, IHostApplicationLifetime applicationLifetime, IOptions<JournalOptions> options) : IDisposable
{
    private readonly ILogger<SystemdJournal>? _systemdLogger = options.Value.EnableLogging ? loggerFactory.CreateLogger<SystemdJournal>() : null;
    private readonly AccessControl _accessControl = new(5);

    [SuppressMessage("Performance", "CA1822:Member als statisch markieren")]
    public IReadOnlyDictionary<string, string> GetFieldDescriptions()
        => JournalFieldDescriptions.FieldDescriptions;

    public async Task<IReadOnlyCollection<string>> GetFields(CancellationToken cancellationToken = default)
        => await _accessControl.Run(() =>
        {
            using var journal = new SystemdJournal(_systemdLogger, options.Value.OpeningMode);
            return journal.EnumerateAvailableFields();
        }, cancellationToken);

    public async Task<IReadOnlyCollection<string>> GetFieldValues(string field, CancellationToken cancellationToken = default)
        => await _accessControl.Run(() =>
        {
            using var journal = new SystemdJournal(_systemdLogger, options.Value.OpeningMode);
            return journal.EnumerateUniqueFieldValues(field);
        }, cancellationToken);

    /// <param name="order">desc or asc</param>
    public async Task<IReadOnlyCollection<IReadOnlyDictionary<string, string>>> GetLogs(DateTimeOffset since, string order = "desc", int limit = 100, string? filter = null, CancellationToken cancellationToken = default)
        => await _accessControl.Run(() =>
        {
            if (limit < 0)
                throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be a positiv value.");
            if (order.ToUpperInvariant() is not "ASC" and not "DESC")
                throw new ArgumentException("Order must be 'asc' or 'desc'.", nameof(order));
            if (since > DateTimeOffset.Now)
                throw new ArgumentOutOfRangeException(nameof(since), "Since must be in the past.");

            using var journal = new SystemdJournal(_systemdLogger, options.Value.OpeningMode);

            try
            {
                // TODO: Use datetime offset in seek realtime
                journal.SeekRealtime(since.DateTime);
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
                {
                    var entry = journal.GetCurrentEntry();

                    if (order == "desc")
                        logs.Insert(0, entry);
                    else
                        logs.Add(entry);
                }

                return logs;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Error reading journal entries.", ex);
            }
        }, cancellationToken);

    // TODO: maybe later
    /// <param name="order">desc or asc</param>
    internal async Task GetLogs(Stream stream, DateTimeOffset since, TimeSpan interval, string? filter = null, CancellationToken cancellationToken = default)
    {
        if (since > DateTimeOffset.Now)
            throw new ArgumentOutOfRangeException(nameof(since), "Since must be in the past.");

        using var journal = new SystemdJournal(_systemdLogger, options.Value.OpeningMode);

        try
        {
            // TODO: Use datetime offset in seek realtime
            journal.SeekRealtime(since.DateTime);
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

        // combine cancellation token
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, applicationLifetime.ApplicationStopping);
        JsonSerializerOptions jsonSerializerOptions = new() { WriteIndented = false, };

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                if (!journal.Next())
                {
                    await Task.Delay(interval, cts.Token);
                    continue;
                }

                var entry = journal.GetCurrentEntry();

                if (entry is null)
                    continue;

                // TODO: may serialize to stream directly
                var json = JsonSerializer.Serialize(entry, jsonSerializerOptions);
                var data = Encoding.UTF8.GetBytes($"{json}\n\n");
                await stream.WriteAsync(data, cts.Token);
                await stream.FlushAsync(cts.Token);
            }

        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error reading journal entries.", ex);
        }
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

        using var journal = new SystemdJournal(_systemdLogger, options.Value.OpeningMode);

        try
        {
            // TODO: Use datetime offset in seek realtime
            journal.SeekRealtime(since.DateTime);
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
            using var journal = new SystemdJournal(_systemdLogger, options.Value.OpeningMode);

            try
            {
                if (!string.IsNullOrEmpty(filter))
                    Filter.Apply(filter, journal);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error applying filters. ({filter})", ex);
            }

            // TODO: Use datetime offset
            try
            {
                return journal.GetPriorityCountsSince(since.DateTime, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error reading priority counts.", ex);
            }
        }, cancellationToken);

    public async Task<IReadOnlyDictionary<int, int>> GetPriorityCounts(DateTimeOffset start, DateTimeOffset end, string? filter = null, CancellationToken cancellationToken = default)
        => await _accessControl.Run(() =>
        {
            using var journal = new SystemdJournal(_systemdLogger, options.Value.OpeningMode);

            try
            {
                if (!string.IsNullOrEmpty(filter))
                    Filter.Apply(filter, journal);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error applying filters. ({filter})", ex);
            }

            // TODO: Use datetime offset
            try
            {
                return journal.GetPriorityCountsBetween(start.DateTime, end.DateTime, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error reading priority counts.", ex);
            }
        }, cancellationToken);

    [SuppressMessage("Reliability", "CA2000:Objekte verwerfen, bevor Bereich verloren geht")]
    public async Task<JournalGenericSession> CreateGeneric(CancellationToken cancellationToken = default)
    {
        var access = await _accessControl.Begin(cancellationToken);
        return new(options.Value, access.Dispose, _systemdLogger);
    }

    [SuppressMessage("Reliability", "CA2000:Objekte verwerfen, bevor Bereich verloren geht")]
    public async Task<JournalFollowSession> CreateFollower(TimeSpan interval, CancellationToken cancellation = default)
    {
        var access = await _accessControl.Begin(cancellation);
        return new(options.Value, interval, access.Dispose, _systemdLogger);
    }

    public void Dispose()
        => _accessControl.Dispose();
}
