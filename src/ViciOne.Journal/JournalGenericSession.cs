using Microsoft.Extensions.Logging;

namespace ViciOne.Journal;

public sealed class JournalGenericSession : IJournalGenericSession
{
    private readonly SystemdJournal _journal;
    private readonly Action _onDispose;

    private bool _positionSet;

    internal JournalGenericSession(JournalOptions options, Action onDispose, ILogger<SystemdJournal>? logger = null)
    {
        _journal = new(logger, options.OpeningMode);
        _onDispose = onDispose;

        try
        {
            _journal.SeekTail();
            _positionSet = true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error seeking journal tail.", ex);
        }
    }

    public void SetFilter(string filter)
    {
        try
        {
            Filter.Apply(filter, _journal);
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
            _journal.SeekTail();
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
            _journal.SeekHead();
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
            _journal.SeekRealtime(timestamp);
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
            _journal.SeekCursor(cursor);
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
            return _journal.GetCursor();
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

        var count = 0;
        Func<bool> next = forward ? () => _journal.Next() : () => _journal.Previous();

        if (skipFirst)
            next();

        while (count < size && next())
        {
            var entry = _journal.GetCurrentEntry();
            if (entry is null)
                break;
            yield return entry;
            count++;
        }
    }

    public void Dispose()
    {
        _journal.Dispose();
        _onDispose();
    }
}
