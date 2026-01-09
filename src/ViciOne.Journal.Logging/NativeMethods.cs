using System.Runtime.InteropServices;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]

namespace ViciOne.Journal;

internal static partial class NativeMethods
{
    private const string SYSTEMD_LIB = "libsystemd.so.0";

    [LibraryImport(SYSTEMD_LIB, SetLastError = true)]
    internal static partial int sd_journal_open(out IntPtr journal, int flags);

    [LibraryImport(SYSTEMD_LIB, SetLastError = true)]
    internal static partial void sd_journal_close(IntPtr journal);

    [LibraryImport(SYSTEMD_LIB, SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int sd_journal_sendv([In] IOVector[] iov, UIntPtr iovcnt);
}
