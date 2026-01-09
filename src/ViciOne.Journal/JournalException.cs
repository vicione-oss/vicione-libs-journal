using System.Diagnostics.CodeAnalysis;

namespace ViciOne.Journal;

/// <summary>
/// Exception class for journal errors; it includes the error code.
/// </summary>
[SuppressMessage("Design", "CA1032:Standardausnahmekonstruktoren implementieren")]
public class JournalException : Exception
{
    public int ErrorCode { get; }

    public JournalException(string message, int errorCode) : base(message)
    {
        ErrorCode = errorCode;
        HResult = errorCode;
    }
}
