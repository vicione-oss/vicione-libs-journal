using System.Runtime.InteropServices;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]

namespace ViciOne.Journal;

internal static partial class NativeMethods
{
    internal const string SYSTEMD_LIB = "libsystemd.so.0";
    internal const string LIBC_LIB = "libc";

    [LibraryImport(SYSTEMD_LIB, SetLastError = true)]
    internal static partial int sd_journal_open(out IntPtr journal, int flags);

    [LibraryImport(SYSTEMD_LIB, SetLastError = true)]
    internal static partial void sd_journal_close(IntPtr journal);

    [LibraryImport(SYSTEMD_LIB, SetLastError = true)]
    internal static partial int sd_journal_next(IntPtr journal);

    [LibraryImport(SYSTEMD_LIB, SetLastError = true)]
    internal static partial int sd_journal_previous(IntPtr journal);

    [LibraryImport(SYSTEMD_LIB, SetLastError = true)]
    internal static partial int sd_journal_add_match(IntPtr journal, [In] byte[] data, UIntPtr size);

    [LibraryImport(SYSTEMD_LIB, StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    internal static partial int sd_journal_seek_head(IntPtr journal);

    [LibraryImport(SYSTEMD_LIB, StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    internal static partial int sd_journal_seek_tail(IntPtr journal);

    [LibraryImport(SYSTEMD_LIB, SetLastError = true)]
    internal static partial int sd_journal_seek_realtime_usec(IntPtr journal, ulong usec);

    // sd_journal_get_data returns a pointer to internal memory and should not be freed manually.
    // The field name must be NUL-terminated UTF-8.
    [LibraryImport(SYSTEMD_LIB)]
    internal static unsafe partial int sd_journal_get_data(IntPtr journal, byte* field, out IntPtr data, out UIntPtr length);

    // Resets the internal pointer for data retrieval of the current entry.
    [LibraryImport(SYSTEMD_LIB, StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    internal static partial void sd_journal_restart_data(IntPtr journal);

    // Allows iteration over all available fields of an entry.
    [LibraryImport(SYSTEMD_LIB, StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    internal static partial int sd_journal_enumerate_data(IntPtr journal, out IntPtr data, out UIntPtr length);

    [LibraryImport(SYSTEMD_LIB, SetLastError = true)]
    internal static partial int sd_journal_get_realtime_usec(IntPtr journal, out ulong usec);

    [LibraryImport(SYSTEMD_LIB, StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    internal static partial int sd_journal_get_cursor(IntPtr journal, out IntPtr cursor);

    [LibraryImport(SYSTEMD_LIB, StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    internal static partial int sd_journal_seek_cursor(IntPtr journal, string cursor);

    [LibraryImport(SYSTEMD_LIB, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int sd_journal_test_cursor(IntPtr journal, string cursor);

    [LibraryImport(SYSTEMD_LIB, SetLastError = true)]
    internal static partial int sd_journal_wait(IntPtr journal, ulong timeout);

    // Functions for listing available fields:
    [LibraryImport(SYSTEMD_LIB, SetLastError = true)]
    internal static partial int sd_journal_enumerate_fields(IntPtr journal, out IntPtr data);

    // Resets the iteration of available fields.
    [LibraryImport(SYSTEMD_LIB, SetLastError = true)]
    internal static partial void sd_journal_restart_fields(IntPtr journal);

    // Functions for querying unique values of a field:
    [LibraryImport(SYSTEMD_LIB, StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    internal static partial int sd_journal_query_unique(IntPtr journal, string field);

    // Resets the internal state for unique field query.
    [LibraryImport(SYSTEMD_LIB, SetLastError = true)]
    internal static partial void sd_journal_restart_unique(IntPtr journal);

    [LibraryImport(SYSTEMD_LIB, SetLastError = true)]
    internal static partial int sd_journal_enumerate_unique(IntPtr journal, out IntPtr data, out UIntPtr length);

    // Import free() from libc for freeing pointers (e.g. in GetCursor and EnumerateUniqueFieldValues).
    [LibraryImport(LIBC_LIB, SetLastError = false)]
    internal static partial void free(IntPtr ptr);

    [LibraryImport(SYSTEMD_LIB, SetLastError = true)]
    internal static partial int sd_journal_add_disjunction(IntPtr journal);

    [LibraryImport(SYSTEMD_LIB)]
    internal static partial void sd_journal_flush_matches(IntPtr journal);
}
