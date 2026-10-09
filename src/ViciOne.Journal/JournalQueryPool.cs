using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace ViciOne.Journal;

/// <summary>
/// Runs one-shot queries on journals that stay open between queries, each on its own thread.
/// </summary>
/// <remarks>
/// Opening a journal maps all of its files, which costs tens of milliseconds on a desktop and more on an edge device.
/// </remarks>
internal sealed class JournalQueryPool : IDisposable
{
    private readonly Func<ISystemdJournal> _openJournal;
    private readonly ILogger<SystemdJournal>? _logger;
    private readonly AccessControl _slots;
    private readonly Stack<JournalThread> _idle = [];
    private readonly Lock _idleLock = new();
    private bool _disposed;

    public JournalQueryPool(Func<ISystemdJournal> openJournal, int size, ILogger<SystemdJournal>? logger)
    {
        _openJournal = openJournal;
        _logger = logger;
        _slots = new(size, $"Only {size} journal queries may run at once.");
    }

    /// <exception cref="JournalBusyException">Thrown when all journals are running a query.</exception>
    [SuppressMessage("Reliability", "CA2000:Objekte verwerfen, bevor Bereich verloren geht", Justification = "The thread goes back to the pool, which disposes it.")]
    public async Task<T> Run<T>(Func<ISystemdJournal, T> query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var slot = _slots.Acquire();
        var thread = TakeIdle() ?? await Task.Run(() => new JournalThread(_openJournal, _logger), cancellationToken);

        try
        {
            var result = await thread.ExecuteAsync(journal =>
            {
                journal.FlushMatches();
                journal.ProcessChanges();
                return query(journal);
            });

            Return(thread);
            return result;
        }
        catch (Exception ex) when (LeavesJournalUsable(ex))
        {
            Return(thread);
            throw;
        }
        catch
        {
            thread.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        lock (_idleLock)
        {
            _disposed = true;
            while (_idle.TryPop(out var thread))
                thread.Dispose();
        }

        _slots.Dispose();
    }

    // Invalid input and cancellation leave the journal as it was, and the next query flushes its matches anyway.
    // A native error can leave it unusable, for example after a failed reinitialization.
    private static bool LeavesJournalUsable(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is JournalException or ObjectDisposedException)
                return false;
        }

        return true;
    }

    private JournalThread? TakeIdle()
    {
        lock (_idleLock)
            return _idle.TryPop(out var thread) ? thread : null;
    }

    private void Return(JournalThread thread)
    {
        lock (_idleLock)
        {
            if (!_disposed)
            {
                _idle.Push(thread);
                return;
            }
        }

        thread.Dispose();
    }
}
