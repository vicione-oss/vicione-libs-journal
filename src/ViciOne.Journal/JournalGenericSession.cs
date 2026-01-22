using Microsoft.Extensions.Logging;

namespace ViciOne.Journal;

public sealed class JournalGenericSession : IJournalGenericSession
{
    private readonly JournalThread _journalThread;
    private readonly Action _onDispose;

    private bool _positionSet;

    internal JournalGenericSession(JournalOptions options, Action onDispose, ILogger<SystemdJournal>? logger = null)
    {
        _journalThread = new JournalThread(options, logger);
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
            _journalThread.Execute(j => Filter.Apply(filter, j));
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

    public void SeekRealTime(DateTime timestamp)
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
    {
        if (size < 1)
            throw new ArgumentOutOfRangeException(nameof(size), "Size must be greater than 0.");

        if (!_positionSet)
            throw new InvalidOperationException("Position not set. Seek a position before reading.");

        // Execute the entire enumeration on the dedicated thread and return results
        var results = _journalThread.Execute(j =>
        {
            var list = new List<IReadOnlyDictionary<string, string>>(size);
            var count = 0;
            Func<bool> next = forward ? () => j.Next() : () => j.Previous();

            if (skipFirst)
                next();

            while (count < size && next())
            {
                var entry = j.GetCurrentEntry();
                if (entry is null)
                    break;
                list.Add(entry);
                count++;
            }

            return list;
        });

        return results;
    }

    public void Dispose()
    {
        _journalThread.Dispose();
        _onDispose();
    }
}
