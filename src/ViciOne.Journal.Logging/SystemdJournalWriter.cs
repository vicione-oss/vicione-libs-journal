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
        var iovecs = CreateIoVectors(args);

        lock (_syncLock)
        {
            var ret = SafeNativeCall(handler => NativeMethods.sd_journal_sendv(iovecs, (UIntPtr)iovecs.Length));
            CheckNativeResult(ret, nameof(NativeMethods.sd_journal_sendv));
        }
    }

    private static IOVector[] CreateIoVectors(IReadOnlyDictionary<string, object?> args)
    {
        var iovecs = new IOVector[args.Count];

        for (var i = 0; i < args.Count; i++)
            iovecs[i] = CreateIoVectorFromString(args.ElementAt(i));

        return iovecs;
    }

    private static IOVector CreateIoVectorFromString(KeyValuePair<string, object?> pair)
    {
        var byteArray = Encoding.UTF8.GetBytes($"{pair.Key}={pair.Value}");
        IOVector iov;
        iov.iov_base = Marshal.UnsafeAddrOfPinnedArrayElement(byteArray, 0);
        iov.iov_len = (UIntPtr)byteArray.Length;

        return iov;
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
