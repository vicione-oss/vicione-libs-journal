namespace ViciOne.Journal;

public static class JournalGenericSessionExtensions
{
    public static JournalLookBehindSession ToLookBehind(this IJournalGenericSession genericSession)
        => new(genericSession);

    public static async Task<JournalLookBehindSession> ToLookBehind<T>(this Task<T> genericSessionTask) where T : IJournalGenericSession
        => new(await genericSessionTask.ConfigureAwait(false));
}
