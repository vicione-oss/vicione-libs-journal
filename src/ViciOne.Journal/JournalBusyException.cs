using System.Diagnostics.CodeAnalysis;

namespace ViciOne.Journal;

/// <summary>
/// Thrown when the configured number of journal sessions or queries is already in use.
/// </summary>
[SuppressMessage("Design", "CA1032:Standardausnahmekonstruktoren implementieren")]
public class JournalBusyException(string message) : InvalidOperationException(message);
