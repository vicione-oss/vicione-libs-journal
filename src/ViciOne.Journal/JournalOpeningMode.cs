namespace ViciOne.Journal;

/// <summary>
/// Flags to control opening of the journal.
/// </summary>
[Flags]
public enum JournalOpeningMode : int
{
    /// <summary>
    /// No special flags (open all accessible journals, both system and user).
    /// </summary>
    None = 0,

    /// <summary>
    /// Only open journal files of the local machine, not from remote hosts.
    /// </summary>
    LocalOnly = 0x01,

    /// <summary>
    /// Only open the runtime (volatile/in-memory) journal, not persistent storage.
    /// </summary>
    RuntimeOnly = 0x02,

    /// <summary>
    /// Only open the system journal (not user journals).
    /// </summary>
    System = 0x04,

    /// <summary>
    /// Only open the journal for the current user.
    /// </summary>
    CurrentUser = 0x08
}
