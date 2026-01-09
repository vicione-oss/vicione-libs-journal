using System.Runtime.InteropServices;
using System.Text;

namespace ViciOne.Journal;

internal class SystemdJournalWriter : IDisposable
{
    private const int MAX_NATIVE_ERRORS = 2;

    private readonly Lock _syncLock = new();
    private SafeJournalHandle _journalHandle;
    private readonly JournalOpeningMode _openMode;
    private int _nativeErrorCount;
    private bool _disposed;

    internal SystemdJournalWriter(JournalOpeningMode openingMode = JournalOpeningMode.LocalOnly)
    {
        _openMode = openingMode;
        _disposed = false;

        if (!Enum.IsDefined(openingMode))
            throw new ArgumentException("Invalid JournalFlags value.", nameof(openingMode));

        var ret = NativeMethods.sd_journal_open(out var handle, (int)openingMode);
        CheckNativeResult(ret, nameof(NativeMethods.sd_journal_open));

        _journalHandle = new(handle);
    }

    public void WriteEntry(IReadOnlyDictionary<string, object?> args)
    {
        if (args.Count == 0)
            return;

        var iovecs = new IOVector[args.Count];
        var handles = new GCHandle[args.Count];
        var i = 0;

        try
        {
            foreach (var (field, value) in args)
            {
                var bytes = Encoding.UTF8.GetBytes($"{field}={value}");
                handles[i] = GCHandle.Alloc(bytes, GCHandleType.Pinned);

                iovecs[i] = new IOVector
                {
                    iov_base = handles[i].AddrOfPinnedObject(),
                    iov_len = (UIntPtr)bytes.Length
                };

                i++;
            }

            lock (_syncLock)
            {
                var ret = NativeMethods.sd_journal_sendv(iovecs, (UIntPtr)i);
                CheckNativeResult(ret, nameof(NativeMethods.sd_journal_sendv));
            }
        }
        finally
        {
            for (var j = 0; j < i; j++)
            {
                if (handles[j].IsAllocated)
                    handles[j].Free();
            }
        }
    }

    private T SafeNativeCall<T>(Func<IntPtr, T> nativeCall)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SystemdJournalWriter), "The journal has already been disposed and cannot be used.");

        var added = false;

        try
        {
            _journalHandle.DangerousAddRef(ref added);
            var handle = _journalHandle.DangerousGetHandle();
            return nativeCall(handle);
        }
        finally
        {
            if (added)
                _journalHandle.DangerousRelease();
        }
    }

    private int CheckNativeResult(int ret, string functionName)
    {
        if (ret < 0)
        {
            _nativeErrorCount++;

            if (_nativeErrorCount >= MAX_NATIVE_ERRORS)
            {
                ReinitializeJournal();

                _nativeErrorCount = 0;

                throw new InvalidOperationException($"{functionName} failed and journal connection reinitialized with error: {ret}");
            }
            else
            {
                throw new InvalidOperationException($"{functionName} failed with error {ret}");
            }
        }
        else
        {
            _nativeErrorCount = 0;
        }

        return ret;
    }

    private void ReinitializeJournal()
    {
        _journalHandle?.Dispose();

        var ret = NativeMethods.sd_journal_open(out var newHandle, (int)_openMode);
        CheckNativeResult(ret, nameof(NativeMethods.sd_journal_open));

        _journalHandle = new(newHandle);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            lock (_syncLock)
            {
                if (_disposed)
                    return;

                _journalHandle.Dispose();

                _disposed = true;
            }
        }
    }
}
