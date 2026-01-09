namespace ViciOne.Journal;

internal interface IJournalFilterService
{
    /// <summary>
    /// Parst den Filter-String (im journald-Syntax) und wendet die Filter auf die angegebene Journal-Instanz an.
    /// Beispiel: "_SYSLOG_IDENTIFIER=sshd+root,_PRIORITY=3,4"
    /// </summary>
    /// <param name="journal">Die zu filternde Journal-Instanz.</param>
    /// <param name="filters">Der Filter-String.</param>
    void ApplyFilters(SystemdJournal journal, string filters);
}
