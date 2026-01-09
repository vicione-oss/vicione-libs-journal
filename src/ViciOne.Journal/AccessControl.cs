namespace ViciOne.Journal;

internal class AccessControl(int limit) : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(limit, limit);

    public void Dispose()
        => _semaphore.Dispose();

    internal async Task<IDisposable> Begin(CancellationToken cancellationToken)
    {
        ThrowIfAccessLimitReached();
        await _semaphore.WaitAsync(cancellationToken);
        return new Disposeable(() => _semaphore.Release());
    }

    internal async Task<T> Run<T>(Func<T> action, CancellationToken cancellationToken = default)
    {
        ThrowIfAccessLimitReached();
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            return action();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void ThrowIfAccessLimitReached()
    {
        if (_semaphore.CurrentCount == 0)
            throw new InvalidOperationException($"Access limit reached. Only {limit} concurrent accesses are permitted.");
    }

    private class Disposeable(Action dispose) : IDisposable
    {
        public void Dispose()
            => dispose();
    }
}
