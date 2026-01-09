using Microsoft.Win32.SafeHandles;

namespace ViciOne.Journal;

/// <summary>
/// A SafeHandle for the journal, based on SafeHandleZeroOrMinusOneIsInvalid.
/// </summary>
internal class SafeJournalHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>
    /// Parameterless constructor for internal use.
    /// </summary>
    internal SafeJournalHandle() : base(true) { }

    /// <summary>
    /// Constructor that sets the internal handle directly.
    /// </summary>
    internal SafeJournalHandle(IntPtr handle) : base(true)
        => SetHandle(handle);

    /// <summary>
    /// Releases the handle. Any errors during release are not propagated as exceptions.
    /// </summary>
    protected override bool ReleaseHandle()
    {
        NativeMethods.sd_journal_close(handle);
        return true;
    }
}
