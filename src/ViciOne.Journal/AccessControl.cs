namespace ViciOne.Journal;

internal sealed class AccessControl(int limit, string busyMessage) : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(limit, limit);

    public void Dispose()
        => _semaphore.Dispose();

    /// <returns>The slot, released when it is disposed for the first time.</returns>
    /// <exception cref="JournalBusyException">Thrown when all slots are taken.</exception>
    internal IDisposable Acquire()
    {
        if (!_semaphore.Wait(0))
            throw new JournalBusyException(busyMessage);

        return new Slot(_semaphore);
    }

    /// <exception cref="JournalBusyException">Thrown when all slots are taken.</exception>
    internal T Run<T>(Func<T> action)
    {
        using var slot = Acquire();
        return action();
    }

    private sealed class Slot(SemaphoreSlim semaphore) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                semaphore.Release();
        }
    }
}
