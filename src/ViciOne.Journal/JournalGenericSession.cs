using Microsoft.Extensions.Logging;

namespace ViciOne.Journal;

public sealed class JournalGenericSession : IJournalGenericSession
{
    private readonly JournalThread _journalThread;
    private readonly Action _onDispose;

    private bool _positionSet;

    internal JournalGenericSession(Func<ISystemdJournal> openJournal, Action onDispose, ILogger<SystemdJournal>? logger = null)
    {
        _journalThread = new JournalThread(openJournal, logger);
        _onDispose = onDispose;

        try
        {
            _journalThread.Execute(j => j.SeekTail());
            _positionSet = true;
        }
        catch (Exception ex)
        {
            _journalThread.Dispose();
            throw new InvalidOperationException("Error seeking journal tail.", ex);
        }
    }

    public void SetFilter(string filter)
    {
        try
        {
            _journalThread.Execute(j =>
            {
                j.FlushMatches();
                Filter.Apply(filter, j);
            });
            _positionSet = false;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error applying filters. ({filter})", ex);
        }
    }

    public void SeekTail()
    {
        try
        {
            _journalThread.Execute(j => j.SeekTail());
            _positionSet = true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error seeking journal tail.", ex);
        }
    }

    public void SeekHead()
    {
        try
        {
            _journalThread.Execute(j => j.SeekHead());
            _positionSet = true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error seeking journal head.", ex);
        }
    }

    /// <param name="timestamp">The time to seek to; <see cref="DateTimeKind.Unspecified"/> is read as local time.</param>
    public void SeekRealTime(DateTime timestamp)
        => SeekRealTime(new DateTimeOffset(timestamp.ToUniversalTime()));

    public void SeekRealTime(DateTimeOffset timestamp)
    {
        try
        {
            _journalThread.Execute(j => j.SeekRealtime(timestamp));
            _positionSet = true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error seeking journal realtime.", ex);
        }
    }

    public void SeekCursor(string cursor)
    {
        try
        {
            _journalThread.Execute(j => j.SeekCursor(cursor));
            _positionSet = true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error seeking journal cursor.", ex);
        }
    }

    public string GetCursor()
    {
        try
        {
            return _journalThread.Execute(j => j.GetCursor());
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error getting journal cursor.", ex);
        }
    }

    public IEnumerable<IReadOnlyDictionary<string, string>> GetLogs(int size, bool forward = true, bool skipFirst = false)
        => Read<IReadOnlyDictionary<string, string>>(size, forward, skipFirst, j => j.GetCurrentEntry());

    /// <summary>
    /// Reads only the given fields of the next entries, which is much cheaper than <see cref="GetLogs"/>.
    /// </summary>
    public IEnumerable<JournalRecord> GetRecords(JournalFieldSet fields, int size, bool forward = true, bool skipFirst = false)
        => Read(size, forward, skipFirst, j => JournalRecordReader.Read(j, fields));

    private List<T> Read<T>(int size, bool forward, bool skipFirst, Func<ISystemdJournal, T> readEntry)
    {
        if (size < 1)
            throw new ArgumentOutOfRangeException(nameof(size), "Size must be greater than 0.");

        if (!_positionSet)
            throw new InvalidOperationException("Position not set. Seek a position before reading.");

        // Execute the entire enumeration on the dedicated thread and return results
        return _journalThread.Execute(j =>
        {
            var list = new List<T>(size);
            Func<bool> next = forward ? j.Next : j.Previous;

            if (skipFirst)
                next();

            while (list.Count < size && next())
                list.Add(readEntry(j));

            return list;
        });
    }

    public void Dispose()
    {
        _journalThread.Dispose();
        _onDispose();
    }
}
