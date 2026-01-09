// Usage Notes
//
// Deterministic Dispose:
// Please ensure that instances of SystemdJournal (e.g., in JournalFieldsProvider and JournalFieldValuesProvider)
// are always used within a using block or explicitly call Dispose().
//
// Conditional Compilation for GetCursor:
// To test the correct variant of memory release, you can define the preprocessor symbol FREE_CURSOR
// (e.g., in the project properties or with #define FREE_CURSOR at the beginning of the file). This allows you to
// choose between the variants (with and without manual release).
//
// Journald Reinitialization:
// In case of repeated (two consecutive) errors when calling native functions, an attempt is automatically made
// to re-establish the connection to journald. In this case, a JournalException is thrown, indicating in its message
// (or via _logger entries) that a reinitialization occurred.
// The calling application should catch this exception and respond accordingly (e.g., restart the journald access
// or take further measures).
//
// _loggerging with Seri_logger:
// Ensure that Seri_logger is initialized (e.g., in your program startup) so that the _logger entries actually appear
// in the desired output.

// Memory Settings
//
// **sd_journal_get_cursor (Flag: `FREE_CURSOR`):**
// The documentation (and the source code of systemd) indicate that the pointer returned by `sd_journal_get_cursor`
// points to an internal buffer. This buffer remains valid until the next journal call and should not be freed by 
// the user.
// 
// **Setting:**
// FREE_CURSOR = false` is correct.
//
// **sd_journal_enumerate_data (Flag: `FREE_ENUMERATE_DATA`)**
// Again, this is a pointer to an internal buffer that is provided during the iteration over an entry's data. User
// deallocation is not intended.
//
// **Setting:**
// FREE_ENUMERATE_DATA = false` is correct.
//
// **sd_journal_get_data (Flag: `FREE_JOURNAL_GET_DATA`)**
// The function `sd_journal_get_data` returns a pointer to internally managed data. There is no requirement to free
// this pointer, as it is not allocated via `malloc()`.
//
// **Setting:**
// FREE_JOURNAL_GET_DATA = false` is correct.
//
// **sd_journal_enumerate_available_fields (Flag: `FREE_AVAILABLE_FIELDS`)**
// Similar to the other data query routines, the pointer returned by `sd_journal_enumerate_available_fields` points 
// to internal memory and should not be freed.
//
// **Setting:**
// FREE_AVAILABLE_FIELDS = false` is correct.
//
// **sd_journal_enumerate_unique (Flag: `FREE_UNIQUE_DATA`)**
// Here, the behavior differs: In some systemd versions (and based on discussions in the community as well as 
// implementations in other bindings, such as Go projects), the return value of `sd_journal_enumerate_unique` is 
// indeed allocated with `malloc()`. This means that the caller—once they have copied the value into safe, managed
// memory—must free this memory using `free()` to avoid memory leaks.
//
// **Setting:**
// FREE_UNIQUE_DATA = true is appropriate.
//
//
// Summary
//
// - **FREE_CURSOR = false**:  
//   The cursor pointer belongs to the library and not to the user.
// 
// - **FREE_ENUMERATE_DATA = false**:  
//   The data pointers from entry enumeration point to internal memory.
// 
// - **FREE_JOURNAL_GET_DATA = false**:  
//   This also uses internal memory that does not need to be freed.
// 
// - **FREE_AVAILABLE_FIELDS = false**:  
//   The available field names reside in an internal memory area and should not be freed.
// 
// - **FREE_UNIQUE_DATA = true**:  
//   The pointers returned by `sd_journal_enumerate_unique` are (at least in common implementations) allocated using `malloc()` and must be freed by the caller using `free()`.
// #define FREE_CURSOR
// #define FREE_ENUMERATE_DATA
// #define FREE_JOURNAL_GET_DATA
// #define FREE_AVAILABLE_FIELDS
// #define FREE_UNIQUE_DATA

using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace ViciOne.Journal;

/// <summary>
/// A convenient wrapper around the systemd sd-journal C API.
/// Provides functions for opening/closing, filtering, navigating, and retrieving journal entries.
/// </summary>
internal sealed partial class SystemdJournal : IDisposable
{
    private const int MAX_NATIVE_ERRORS = 2;

    private SafeJournalHandle _journalHandle;
    private bool _disposed;

    // Synchronization object for thread-safe access.
    private readonly Lock _syncLock = new();
    private readonly ILogger<SystemdJournal>? _logger;

    // For automatic reinitialization.
    private readonly JournalOpeningMode _journalOpeningMode;
    private int _nativeErrorCount;

    /// <summary>
    /// Helper method to safely perform a native call with the journal handle.
    /// It fixes the handle by calling DangerousAddRef before the call and DangerousRelease afterwards.
    /// </summary>
    /// <typeparam name="T">The return type of the native call.</typeparam>
    /// <param name="nativeCall">A delegate that receives the fixed raw handle and returns a value.</param>
    /// <returns>The result of the native call.</returns>
    private T SafeNativeCall<T>(Func<IntPtr, T> nativeCall)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SystemdJournal), "The journal has already been disposed and cannot be used.");

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

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemdJournal"/> class with the specified flags.
    /// </summary>
    /// <param name="flags">Flags controlling which journal to open (default is LocalOnly).</param>
    /// <exception cref="ArgumentException">Thrown when an invalid flag is provided.</exception>
    /// <exception cref="JournalException">Thrown when opening the journal fails.</exception>
    internal SystemdJournal(ILogger<SystemdJournal>? logger = null, JournalOpeningMode flags = JournalOpeningMode.LocalOnly)
    {
        _logger = logger;
        _journalOpeningMode = flags;

        // Ensure that the provided flags are valid.
        if (!Enum.IsDefined(flags))
            throw new ArgumentException("Invalid JournalFlags value.", nameof(flags));

        var ret = NativeMethods.sd_journal_open(out var handle, (int)flags);

        if (ret < 0 || handle == IntPtr.Zero)
            throw new JournalException($"sd_journal_open failed with error {ret}", ret);

        // Embed the handle directly in the SafeJournalHandle.
        _journalHandle = new(handle);
        logger?.LogJournalOpened(flags);
    }

    /// <summary>
    /// Disposes the journal, releasing all resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            lock (_syncLock)
            {
                if (_disposed)
                    return;

                // free managed resources
                _journalHandle?.Dispose();
                _logger?.LogJournalDisposed();

                // unmanaged resources are freed in the SafeJournalHandle
                // and the finalizer, so no additional release is needed here.
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Reinitializes the journal connection (e.g. after consecutive native errors).
    /// </summary>
    /// <exception cref="JournalException">Thrown if reinitialization fails.</exception>
    /// <remarks>
    /// This method is called automatically after two consecutive native errors.
    /// </remarks>
    /// <seealso cref="MAX_NATIVE_ERRORS"/>
    private void ReinitializeJournal()
    {
        _logger?.LogReinitializingJournal();

        // Dispose the old handle
        if (_journalHandle != null)
        {
            try
            {
                _journalHandle.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogErrorDisposingOldHandle(ex);
            }
        }

        var ret = NativeMethods.sd_journal_open(out var newHandle, (int)_journalOpeningMode);
        if (ret < 0 || newHandle == IntPtr.Zero)
        {
            _logger?.LogFailedReinitializeJournal(ret);
            throw new JournalException($"sd_journal_open (reinitialization) failed with error {ret}", ret);
        }

        _journalHandle = new(newHandle);
        _logger?.LogJournalReinitialized();
    }

    /// <summary>
    /// Adds a match to the journal query. The match must be in the format 'FIELD=VALUE'.
    /// </summary>
    /// <param name="match"></param>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="JournalException"></exception>
    /// <remarks>
    /// The field name must consist of uppercase letters, digits, and underscores.
    /// The value can be anything.
    /// If the value contains spaces, it must be enclosed in quotes.
    /// </remarks>
    public void AddMatch(string match)
    {
        _logger?.LogAddMatch(match);

        // Validate input syntax and parameters.
        if (string.IsNullOrWhiteSpace(match))
            throw new ArgumentException("Match cannot be null or whitespace.", nameof(match));

        if (match.Contains('\0', StringComparison.Ordinal))
            throw new ArgumentException("Match cannot contain null characters.", nameof(match));

        // Ensure exactly one '=' is present.
        var equalIndex = match.IndexOf('=', StringComparison.Ordinal);
        if (equalIndex == -1)
            throw new ArgumentException("Match must contain exactly one '=' character.", nameof(match));

        if (equalIndex == 0 || equalIndex == match.Length - 1)
            throw new ArgumentException("Match must have the format 'FIELD=VALUE' with a valid field and value.", nameof(match));

        // Extract field name and value efficiently (avoids unnecessary string allocations)
        var matchSpan = match.AsSpan();
        var fieldSpan = matchSpan[..equalIndex];
        var valueSpan = matchSpan[(equalIndex + 1)..];

        var field = fieldSpan.ToString();
        var value = valueSpan.ToString();

        // Validate field name using precompiled regex
        if (!FieldRegex().IsMatch(field))
            throw new ArgumentException($"Invalid field format: {field}. Only uppercase letters, digits, and underscores are allowed.", nameof(match));

        // If the value contains spaces, it must be enclosed in quotes.
        if (value.Contains(' ', StringComparison.Ordinal) && !(value.StartsWith('"') && value.EndsWith('"')))
            throw new ArgumentException("Values containing spaces must be enclosed in quotes.", nameof(match));

        lock (_syncLock)
        {
            var matchBytes = Encoding.UTF8.GetBytes(match + "\0");
            var ret = SafeNativeCall(
                handle => NativeMethods.sd_journal_add_match(
                    handle,
                    matchBytes,
                    UIntPtr.Zero
                )
            );
            CheckNativeResult(ret, "sd_journal_add_match");
        }
    }

    public void AddDisjunction()
    {
        var ret = SafeNativeCall(NativeMethods.sd_journal_add_disjunction);
        if (ret < 0)
        {
            _nativeErrorCount++;
            _logger?.LogAddDisjunctionFailed(ret);
            throw new JournalException($"sd_journal_add_disjunction failed with error {ret}", ret);
        }
    }

    /// <summary>
    /// Adds a match to the journal query. The match must be in the format 'FIELD=VALUE'.
    /// </summary>
    /// <param name="field">The field name to match.</param>
    /// <param name="value">The value to match.</param>
    /// <exception cref="ArgumentException">Thrown when the field or value is invalid.</exception>
    /// <exception cref="JournalException">Thrown when adding the match fails.</exception>
    /// <remarks>
    /// The field name must consist of uppercase letters, digits, and underscores.
    /// The value can be anything.
    /// If the value contains spaces, it must be enclosed in quotes.
    /// </remarks>
    public void AddMatch(string field, string value)
        => AddMatch($"{field}={value}");

    /// <summary>
    /// Seeks to the beginning of the journal.
    /// </summary>
    /// <param name="timeout">
    /// An optional timeout period to wait for the journal to become ready. The specified timeout is a <see cref="TimeSpan"/>
    /// that is internally converted to microseconds. If the wait operation times out, a <see cref="JournalException"/> is thrown.
    /// </param>
    /// <exception cref="JournalException">
    /// Thrown if the underlying sd-journal_seek_head operation fails or if the operation times out.
    /// </exception>
    public void SeekHead(TimeSpan? timeout = null)
    {
        _logger?.LogSeekHead();

        lock (_syncLock)
        {
            if (timeout.HasValue)
            {
                // Convert timeout to microseconds
                var timeoutUsec = (ulong)timeout.Value.TotalMilliseconds * 1000;
                var waitRet = SafeNativeCall(
                    handle => NativeMethods.sd_journal_wait(
                        handle,
                        timeoutUsec
                    )
                );
                if (waitRet == 0)
                    throw new JournalException("SeekHead operation timed out", -1);
            }

            var ret = SafeNativeCall(NativeMethods.sd_journal_seek_head);
            CheckNativeResult(ret, "sd_journal_seek_head");
        }
    }

    /// <summary>
    /// Seeks to the end of the journal.
    /// </summary>
    /// <param name="timeout"></param>
    /// <exception cref="JournalException"></exception>
    /// <remarks>
    /// The timeout parameter is optional and specifies the maximum time to wait for the operation to complete.
    /// The recommended timeout is 500 milliseconds.
    /// If the operation times out, a JournalException is thrown with an error code of -1.
    /// </remarks>
    public void SeekTail(TimeSpan? timeout = null)
    {
        _logger?.LogSeekTail();

        lock (_syncLock)
        {
            if (timeout.HasValue)
            {
                var timeoutUsec = (ulong)timeout.Value.TotalMilliseconds * 1000;
                var waitRet = SafeNativeCall(
                    handle => NativeMethods.sd_journal_wait(
                        handle,
                        timeoutUsec
                    )
                );
                if (waitRet == 0)
                    throw new JournalException("SeekTail operation timed out", -1);
            }

            var ret = SafeNativeCall(NativeMethods.sd_journal_seek_tail);
            CheckNativeResult(ret, "sd_journal_seek_tail");
        }
    }

    /// <summary>
    /// Seeks to the specified timestamp in the journal.
    /// </summary>
    /// <param name="time">The timestamp to seek to.</param>
    /// <param name="timeout">The maximum time to wait for the operation to complete.</param>
    /// <exception cref="ArgumentException">Thrown when the timestamp is invalid.</exception>
    /// <exception cref="JournalException">Thrown when seeking fails.</exception>
    /// <remarks>
    /// The timestamp must be in UTC and not before 1970.
    /// The timeout parameter is optional and specifies the maximum time to wait for the operation to complete.
    /// The recommended timeout is 500 milliseconds.
    /// If the operation times out, a JournalException is thrown with an error code of -1.
    /// </remarks>
    /// <seealso cref="MAX_NATIVE_ERRORS"/>
    public void SeekRealtime(DateTime time, TimeSpan? timeout = null)
    {
        _logger?.LogSeekRealtime(time);

        var utcTime = ValidateAndConvertToUtc(time);
        DateTime epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var usec = (ulong)((utcTime - epoch).TotalMilliseconds * 1000);
        if (usec > (ulong)DateTime.MaxValue.Ticks)
            throw new ArgumentException("The timestamp is too large and would cause an overflow.", nameof(time));

        lock (_syncLock)
        {
            if (timeout.HasValue)
            {
                var timeoutUsec = (ulong)timeout.Value.TotalMilliseconds * 1000;
                var waitRet = SafeNativeCall(
                    handle => NativeMethods.sd_journal_wait(
                        handle,
                        timeoutUsec
                    )
                );
                if (waitRet == 0)
                    throw new JournalException("SeekRealtime operation timed out", -1);
            }
            var ret = SafeNativeCall(handle => NativeMethods.sd_journal_seek_realtime_usec(handle, usec));
            CheckNativeResult(ret, "sd_journal_seek_realtime_usec");
        }
    }

    /// <summary>
    /// Seeks to the specified cursor in the journal.
    /// </summary>
    /// <param name="cursor">The cursor to seek to.</param>
    /// <param name="timeout">The maximum time to wait for the operation to complete.</param>
    /// <exception cref="ArgumentException">Thrown when the cursor is invalid.</exception>
    /// <exception cref="JournalException">Thrown when seeking fails.</exception>
    /// <remarks>
    /// The cursor must not be null, empty, or whitespace.
    /// The cursor must not contain null characters.
    /// The cursor must not exceed 512 characters.
    /// The cursor must not contain special characters like '!@#$%^&*()[]{}<>'
    /// The timeout parameter is optional and specifies the maximum time to wait for the operation to complete.
    /// The recommended timeout is 500 milliseconds.
    /// If the operation times out, a JournalException is thrown with an error code of -1.
    /// </remarks>
    /// <seealso cref="MAX_NATIVE_ERRORS"/>
    public void SeekCursor(string cursor, TimeSpan? timeout = null)
    {
        _logger?.LogSeekCursor(cursor);

        if (string.IsNullOrWhiteSpace(cursor))
            throw new ArgumentException("Cursor cannot be null or whitespace.", nameof(cursor));
        if (cursor.Contains('\0', StringComparison.Ordinal))
            throw new ArgumentException("Cursor cannot contain null characters.", nameof(cursor));

        if (cursor.Length > 512)
            throw new ArgumentException("Cursor is too long. Maximum 512 characters allowed.", nameof(cursor));

        if (cursor.Any("!@#$%^&*()[]{}<>".Contains))
            throw new ArgumentException("Cursor contains invalid characters.", nameof(cursor));

        lock (_syncLock)
        {
            if (timeout.HasValue)
            {
                var timeoutUsec = (ulong)timeout.Value.TotalMilliseconds * 1000;
                var waitRet = SafeNativeCall(
                    handle => NativeMethods.sd_journal_wait(
                        handle,
                        timeoutUsec
                    )
                );
                if (waitRet == 0)
                    throw new JournalException("SeekCursor operation timed out", -1);
            }

            var ret = SafeNativeCall(handle => NativeMethods.sd_journal_seek_cursor(handle, cursor));
            CheckNativeResult(ret, "sd_journal_seek_cursor");
        }
    }

    /// <summary>
    /// Advances to the next journal entry.
    /// </summary>
    /// <param name="timeout">The maximum time to wait for the operation to complete.</param>
    /// <returns>True if the operation was successful and the cursor was advanced; otherwise, false.</returns>
    /// <exception cref="JournalException">Thrown when advancing the cursor fails.</exception>
    /// <remarks>
    /// The timeout parameter is optional and specifies the maximum time to wait for the operation to complete.
    /// The recommended timeout is 500 milliseconds.
    /// If the operation times out, a JournalException is thrown with an error code of -1.
    /// </remarks>
    /// <seealso cref="MAX_NATIVE_ERRORS"/>
    public bool Next(TimeSpan? timeout = null)
    {
        _logger?.LogNextCalled();

        lock (_syncLock)
        {
            if (timeout.HasValue)
            {
                var timeoutUsec = (ulong)timeout.Value.TotalMilliseconds * 1000;
                var waitRet = SafeNativeCall(handle => NativeMethods.sd_journal_wait(handle, timeoutUsec));
                if (waitRet == 0)
                    return false;
            }

            var ret = SafeNativeCall(NativeMethods.sd_journal_next);
            CheckNativeResult(ret, "sd_journal_next");
            return ret > 0;
        }
    }

    public bool Previous(TimeSpan? timeout = null)
    {
        _logger?.LogPreviousCalled();

        lock (_syncLock)
        {
            if (timeout.HasValue)
            {
                var timeoutUsec = (ulong)timeout.Value.TotalMilliseconds * 1000;
                var waitRet = SafeNativeCall(handle => NativeMethods.sd_journal_wait(handle, timeoutUsec));
                if (waitRet == 0)
                    return false;
            }

            var ret = SafeNativeCall(NativeMethods.sd_journal_previous);
            CheckNativeResult(ret, "sd_journal_previous");
            return ret > 0;
        }
    }

    /// <summary>
    /// Retrieves the data of the current journal entry.
    /// Because the data in the journal is sometimes incomplete, the timestamp is always retrieved
    /// directly and added to the entry.
    /// </summary>
    /// <returns>A dictionary containing the field names and values of the current entry.</returns>
    /// <exception cref="JournalException">Thrown when retrieving the data fails.</exception>
    /// <remarks>
    /// The field names must not exceed 256 characters, and the values must not exceed 1024 characters.
    /// If a field has no value, an empty string is returned.
    /// </remarks>
    /// <seealso cref="MAX_NATIVE_ERRORS"/>
    public unsafe Dictionary<string, string> GetCurrentEntry()
    {
        _logger?.LogGetCurrentEntry();

        lock (_syncLock)
        {
            Dictionary<string, string> entry = new(16);
            // Reset the data pointer for the current entry.
            SafeNativeCall(handle =>
            {
                NativeMethods.sd_journal_restart_data(handle);
                return 0;
            });

            while (true)
            {
                var dataPtr = IntPtr.Zero;
                var length = UIntPtr.Zero;
                var ret = SafeNativeCall(
                    handle => NativeMethods.sd_journal_enumerate_data(
                        handle,
                        out dataPtr,
                        out length
                    )
                );
                if (ret < 0)
                {
                    _logger?.LogEnumerateDataFailed(ret);
                    throw new JournalException("sd_journal_enumerate_data failed", ret);
                }
                if (ret == 0 || dataPtr == IntPtr.Zero)
                {
                    break;
                }

                var len = (int)length;
                if (len <= 1)
                    continue;

                var dataSpan = new ReadOnlySpan<byte>(
                    dataPtr.ToPointer(),
                    len
                );
                var separatorIndex = dataSpan.IndexOf((byte)'=');

                if (separatorIndex > 0 && separatorIndex < len - 1)
                {
                    var key = Encoding.UTF8
                        .GetString(dataSpan[..separatorIndex])
                        .Trim();
                    var value = Encoding.UTF8
                        .GetString(dataSpan[(separatorIndex + 1)..])
                        .Trim();

                    //if (key.Length > 256)
                    //{
                    //    throw new JournalException(
                    //        $"Key '{key}' is too long (max. 256 characters allowed).",
                    //        -1
                    //    );
                    //}

                    //if (value.Length > 1024)
                    //{
                    //    throw new JournalException(
                    //        $"Value for '{key}' is too long (max. 1024 characters allowed).",
                    //        -1
                    //    );
                    //}

                    // Regardless of the read value, the timestamp will be overwritten later.
                    entry[key] = value;
                }
                else if (separatorIndex == -1)
                {
                    var key = Encoding.UTF8.GetString(dataSpan).Trim();
                    //if (key.Length > 256)
                    //{
                    //    throw new JournalException(
                    //        $"Key '{key}' is too long (max. 256 characters allowed).",
                    //        -1
                    //    );
                    //}

                    entry[key] = string.Empty;
                }
            }

            if (entry.Count > 0)
            {
                // Sometimes timestamps are missing in the log, so always retrieve the timestamp and insert
                // it into the entry.
                var realtimeTimestamp = GetRealtimeTimestamp();
                // Here the key "REALTIME_TIMESTAMP" is set – it replaces any existing values.
                entry["REALTIME_TIMESTAMP"] = realtimeTimestamp.ToString("o"); // ISO 8601-Format 
            }
            else
            {
                _logger?.LogReadEmptyEntry();
            }

            return entry;
        }
    }

    /// <summary>
    /// Reads all journal entries and returns them as a sequence of dictionaries.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>
    /// An enumerable sequence of dictionaries, where each dictionary contains the field names 
    /// and values of a journal entry.
    /// </returns>
    /// <exception cref="JournalException">Thrown when reading the entries fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    /// <remarks>
    /// The field names must not exceed 256 characters, and the values must not exceed 1024 characters.
    /// If a field has no value, an empty string is returned.
    /// </remarks>
    public IEnumerable<Dictionary<string, string>> ReadEntries(CancellationToken cancellationToken = default)
    {
        lock (_syncLock)
            SeekHead(); // Ensure the cursor is set to the beginning

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Dictionary<string, string> currentEntry;
            lock (_syncLock)
            {
                if (!Next())
                    yield break;
                currentEntry = GetCurrentEntry();
            }

            if (currentEntry != null && currentEntry.Count > 0)
                yield return currentEntry;
        }
    }

    /// <summary>
    /// Retrieves the cursor of the current journal entry.
    /// </summary>
    /// <returns>The cursor of the current entry.</returns>
    /// <exception cref="JournalException">Thrown when retrieving the cursor fails.</exception>
    /// <remarks>
    /// The cursor must not contain null characters and must not exceed 512 characters.
    /// </remarks>
    /// <seealso cref="MAX_NATIVE_ERRORS"/>
    public string GetCursor()
    {
        _logger?.LogGetCursor();

        lock (_syncLock)
        {
            // Perform a single native call and store the pointer.
            var cursorPtr = IntPtr.Zero;
            var ret = SafeNativeCall(handle => NativeMethods.sd_journal_get_cursor(handle, out cursorPtr));
            CheckNativeResult(ret, "sd_journal_get_cursor");

            try
            {
                var cursor = cursorPtr == IntPtr.Zero
                    ? string.Empty
                    : Marshal.PtrToStringUTF8(cursorPtr)?.Trim() ?? string.Empty;

                if (cursor.Contains('\0', StringComparison.Ordinal))
                    throw new JournalException("Invalid cursor: Contains null characters.", -1);
                if (cursor.Length > 512)
                    throw new JournalException("Cursor is too long. Maximum 512 characters allowed.", -1);

                return cursor;
            }
            finally
            {
                NativeMethods.free(cursorPtr);
            }
        }
    }

    /// <summary>
    /// Retrieves the current journal entry's timestamp.
    /// </summary>
    /// <returns>The timestamp of the current entry.</returns>
    /// <exception cref="JournalException">Thrown when retrieving the timestamp fails.</exception>
    /// <remarks>
    /// The timestamp is returned in UTC.
    /// </remarks>
    /// <seealso cref="MAX_NATIVE_ERRORS"/>
    public DateTime GetRealtimeTimestamp()
    {
        _logger?.LogGetRealtimeTimestamp();

        lock (_syncLock)
        {
            ulong usec = 0;
            var ret = SafeNativeCall(handle =>
            {
                var r = NativeMethods.sd_journal_get_realtime_usec(handle, out var result);
                usec = result;
                return r;
            });
            CheckNativeResult(ret, "sd_journal_get_realtime_usec");

            if (usec > (ulong)DateTime.MaxValue.Ticks / 10)
            {
                throw new JournalException("The timestamp is too large and would cause an overflow.", -1);
            }
            DateTime epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddTicks((long)usec * 10);
        }
    }

    /// <summary>
    /// Enumerates all available fields in the journal.
    /// </summary>
    /// <returns>A list of available field names.</returns>
    /// <exception cref="JournalException">Thrown when enumerating the fields fails.</exception>
    /// <remarks>
    /// The field names must consist of uppercase letters, digits, and underscores.
    /// The field names must not exceed 256 characters.
    /// </remarks>
    public List<string> EnumerateAvailableFields()
    {
        _logger?.LogEnumerateAvailableFields();

        lock (_syncLock)
        {
            List<string> fields = [];
            // Removed error check since sd_journal_restart_fields now returns void.
            SafeNativeCall(handle =>
            {
                NativeMethods.sd_journal_restart_fields(handle);
                return 0;
            });

            while (true)
            {
                var dataPtr = IntPtr.Zero;
                var enumRet = SafeNativeCall(
                    handle => NativeMethods.sd_journal_enumerate_fields(
                        handle,
                        out dataPtr
                    )
                );
                if (enumRet < 0)
                {
                    _logger?.LogEnumerateFieldsFailed(enumRet);
                    throw new JournalException("sd_journal_enumerate_fields failed", enumRet);
                }
                if (enumRet == 0)
                    break;

                if (dataPtr != IntPtr.Zero)
                {
                    var field = Marshal.PtrToStringUTF8(dataPtr)?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(field))
                        continue;
                    if (!FieldRegex().IsMatch(field))
                        throw new JournalException($"Invalid field format: {field}.", -1);
                    fields.Add(field);
                }
            }
            return fields;
        }
    }

    /// <summary>
    /// Enumerates all unique values of a specific field in the journal.
    /// </summary>
    /// <param name="field">The field name to enumerate.</param>
    /// <returns>A list of unique field values.</returns>
    /// <exception cref="ArgumentException">Thrown when the field name is invalid.</exception>
    /// <exception cref="JournalException">Thrown when enumerating the field values fails.</exception>
    /// <remarks>
    /// The field name must consist of uppercase letters, digits, and underscores.
    /// The field name must not exceed 64 characters.
    /// The field value must not exceed 1024 characters.
    /// </remarks>
    public List<string> EnumerateUniqueFieldValues(string field)
    {
        _logger?.LogEnumerateUniqueFieldValues(field);

        lock (_syncLock)
        {
            if (string.IsNullOrWhiteSpace(field))
                throw new ArgumentException("The field name cannot be null or empty.", nameof(field));
            if (!FieldRegex().IsMatch(field))
                throw new ArgumentException($"Invalid field format: {field}. Only uppercase letters, digits, and underscores are allowed.", nameof(field));

            var values = new List<string>();

            var queryResult = SafeNativeCall(handle => NativeMethods.sd_journal_query_unique(handle, field));

            if (queryResult < 0)
            {
                _logger?.LogQueryUniqueFailed(field, queryResult);
                throw new JournalException($"sd_journal_query_unique failed for field {field} with error {queryResult}", queryResult);
            }

            // Removed error check since sd_journal_restart_unique now returns void.
            SafeNativeCall(handle =>
            {
                NativeMethods.sd_journal_restart_unique(handle);
                return 0;
            });

            while (true)
            {
                var dataPtr = IntPtr.Zero;
                var length = UIntPtr.Zero;
                var enumRet = SafeNativeCall(
                    handle => NativeMethods.sd_journal_enumerate_unique(
                        handle,
                        out dataPtr,
                        out length
                    )
                );
                if (enumRet < 0)
                {
                    _logger?.LogEnumerateUniqueFailed(enumRet);
                    throw new JournalException("sd_journal_enumerate_unique failed", enumRet);
                }
                if (enumRet == 0)
                    break;

                if (dataPtr != IntPtr.Zero)
                {
                    var len = (int)length;

                    if (len > 1)
                    {
                        var value = Marshal.PtrToStringUTF8(dataPtr, len)?.Trim() ?? string.Empty;
                        //if (value.Length > 1024)
                        //{
                        //    throw new JournalException(
                        //        $"The value '{value}' is too long (max. 1024 characters allowed).",
                        //        -1
                        //    );
                        //}

                        values.Add(value);
                    }
                }
            }

            return values;
        }
    }

    /// <summary>
    /// Retrieves the count of journal entries grouped by priority since a specific timestamp.
    /// </summary>
    /// <param name="since">The timestamp (in UTC) from which to count entries.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>
    /// A dictionary where the keys represent priority levels (0–7) and the values represent the count of occurrences.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the timestamp is invalid (must be in UTC and not before 1970).
    /// </exception>
    /// <exception cref="JournalException">Thrown when an error occurs while counting the entries.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    /// <remarks>
    /// The timestamp must be in UTC format and not earlier than January 1, 1970.
    /// Journal entries are counted only if they contain a `_PRIORITY` field with a valid priority level (0–7).
    /// </remarks>
    /// <seealso cref="MAX_NATIVE_ERRORS"/>
    public Dictionary<int, int> GetPriorityCountsSince(DateTime since, CancellationToken cancellationToken = default)
    {
        _logger?.LogGetPriorityCountsSince(since);

        lock (_syncLock)
        {
            Dictionary<int, int> priorityCounts = new()
            {
                { 0, 0 },
                { 1, 0 },
                { 2, 0 },
                { 3, 0 },
                { 4, 0 },
                { 5, 0 },
                { 6, 0 },
                { 7, 0 },
            };

            var utcSince = ValidateAndConvertToUtc(since, maxTime: DateTime.UtcNow);

            SeekHead();
            SeekRealtime(utcSince);

            while (Next())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var dataPtr = IntPtr.Zero;
                var length = UIntPtr.Zero;
                var ret = SafeNativeCall(
                    handle => NativeMethods.sd_journal_get_data(
                        handle,
                        "PRIORITY",
                        out dataPtr,
                        out length
                    )
                );
                if (ret < 0)
                {
                    // Ignore errors as the entry might not have a _PRIORITY field.
                    continue;
                }
                if (dataPtr == IntPtr.Zero || length == UIntPtr.Zero)
                    continue;

                // Read the data as a string (removing the trailing null character)
                var data = Marshal.PtrToStringUTF8(dataPtr, (int)length)?.Trim() ?? string.Empty;
                var equalIndex = data.IndexOf('=', StringComparison.Ordinal);
                if (equalIndex >= 0 && equalIndex < data.Length - 1)
                {
                    var priorityStr = data[(equalIndex + 1)..];
                    if (int.TryParse(priorityStr, out var priority))
                    {
                        if (priority is < 0 or > 7)
                            continue;
                        // TODO: log if priority is unknown
                        priorityCounts[priority]++;
                    }
                }
            }

            return priorityCounts;
        }
    }

    /// <summary>
    /// Retrieves the counts of journal entries grouped by _PRIORITY within a specified time interval.
    /// Only entries with a realtime timestamp between <paramref name="start"/> (inclusive)
    /// and <paramref name="end"/> (exclusive) are processed.
    /// </summary>
    /// <param name="start">The start timestamp (inclusive) of the interval, in UTC or local time.</param>
    /// <param name="end">The end timestamp (exclusive) of the interval, in UTC or local time.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>
    /// A dictionary where the keys represent priority levels (0–7) and the values represent the count of occurrences.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="start"/> is after <paramref name="end"/> or if the provided timestamps are invalid.
    /// </exception>
    /// <exception cref="JournalException">Thrown if native calls fail while accessing journal data.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
    /// <remarks>
    /// - The provided timestamps are automatically converted to UTC if necessary.
    /// - The timestamp range must be valid: <paramref name="start"/> must be before <paramref name="end"/>.
    /// - The journal entries are filtered based on their _PRIORITY field, which must be an integer between 0 and 7.
    /// - Entries without a _PRIORITY field or with invalid values are ignored.
    /// </remarks>
    /// <seealso cref="MAX_NATIVE_ERRORS"/>
    public Dictionary<int, int> GetPriorityCountsBetween(DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        _logger?.LogGetPriorityCountsBetween(start, end);

        // Convert times to UTC for consistent comparisons.
        var utcStart = ValidateAndConvertToUtc(start);
        var utcEnd = ValidateAndConvertToUtc(end);

        if (utcStart >= utcEnd)
            throw new ArgumentException("Start timestamp must be earlier than end timestamp.");

        Dictionary<int, int> priorityCounts = [];

        lock (_syncLock)
        {
            // Reset the internal cursor and position it to the start time.
            SeekHead();
            SeekRealtime(utcStart);

            // Iterate over all entries from the start time.
            while (Next())
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Retrieve the timestamp of the current entry.
                var entryTime = GetRealtimeTimestamp();
                if (entryTime >= utcEnd)
                {
                    // End the iteration as soon as the entry is outside the desired time window.
                    break;
                }

                // Attempt to retrieve the _PRIORITY field.
                var dataPtr = IntPtr.Zero;
                var length = UIntPtr.Zero;
                var ret = SafeNativeCall(
                    handle => NativeMethods.sd_journal_get_data(
                        handle,
                        "PRIORITY",
                        out dataPtr,
                        out length
                    )
                );
                if (ret < 0)
                {
                    // If the field is missing or an error occurs, skip this entry.
                    continue;
                }
                if (dataPtr == IntPtr.Zero || length == UIntPtr.Zero)
                    continue;

                // The returned string has the format "_PRIORITY=<value>\0"
                var data = Marshal.PtrToStringUTF8(dataPtr, (int)length)?.Trim() ?? string.Empty;

                var equalIndex = data.IndexOf('=', StringComparison.Ordinal);
                if (equalIndex >= 0 && equalIndex < data.Length - 1)
                {
                    var priorityStr = data[(equalIndex + 1)..];
                    if (int.TryParse(priorityStr, out var priority))
                    {
                        // Priority values should be in the range 0 to 7.
                        if (priority is < 0 or > 7)
                            continue;
                        // TODO: log if priority is unknown
                        priorityCounts[priority]++;
                    }
                }
            }
        }

        _logger?.LogGetPriorityCountsBetweenFinished(JsonSerializer.Serialize(priorityCounts));
        return priorityCounts;
    }

    /// <summary>
    /// Checks the result of a native call. If an error occurs, it increments the native error count
    /// and, if a threshold is reached, reinitializes the journal connection.
    /// </summary>
    /// <param name="ret">The return value of the native call.</param>
    /// <param name="functionName">The name of the native function (for logging).</param>
    /// <returns>The original return value if no error occurred.</returns>
    /// <exception cref="JournalException">Throws an exception if the call fails.</exception>
    private int CheckNativeResult(int ret, string functionName)
    {
        if (ret < 0)
        {
            _nativeErrorCount++;
            _logger?.LogNativeCallFailed(functionName, ret, _nativeErrorCount);
            if (_nativeErrorCount >= MAX_NATIVE_ERRORS)
            {
                _logger?.LogReinitializeJournal(functionName);
                ReinitializeJournal();
                _nativeErrorCount = 0;
                throw new JournalException($"{functionName} failed and journal connection reinitialized", ret);
            }
            else
            {
                throw new JournalException($"{functionName} failed with error {ret}", ret);
            }
        }
        else
        {
            _nativeErrorCount = 0;
        }
        return ret;
    }

    /// <summary>
    /// Converts a given <see cref="DateTime"/> value to UTC and ensures it falls within specified boundaries.
    /// </summary>
    /// <param name="time">The date and time value to validate and convert.</param>
    /// <param name="minTime">
    /// The minimum allowed timestamp. Defaults to Unix Epoch (1970-01-01T00:00:00Z) if not specified.
    /// </param>
    /// <param name="maxTime">
    /// The maximum allowed timestamp. Defaults to the current UTC time plus 10 years if not specified.
    /// </param>
    /// <returns>The validated <see cref="DateTime"/> value in UTC format.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown if the provided <paramref name="time"/> is earlier than <paramref name="minTime"/> 
    /// or later than <paramref name="maxTime"/>.
    /// </exception>
    private static DateTime ValidateAndConvertToUtc(DateTime time, DateTime? minTime = null, DateTime? maxTime = null)
    {
        DateTime epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        minTime ??= epoch;
        maxTime ??= DateTime.UtcNow.AddYears(10);

        var utcTime = time.Kind == DateTimeKind.Utc ? time : time.ToUniversalTime();

        if (utcTime < minTime.Value)
            throw new ArgumentException($"Timestamp {utcTime:o} is before the minimum allowed time {minTime:o}.", nameof(time));

        if (utcTime > maxTime.Value)
            throw new ArgumentException($"Timestamp {utcTime:o} exceeds the maximum allowed time {maxTime:o}.", nameof(time));

        return utcTime;
    }

    [GeneratedRegex(@"^(?!.*__)(?=.*[A-Z0-9])[A-Z0-9_]+$", RegexOptions.Compiled)]
    internal static partial Regex FieldRegex();
}
