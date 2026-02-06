
namespace ViciOne.Journal;

public interface IJournalService : IDisposable
{
    Task<IJournalFollowSession> CreateFollower(TimeSpan interval, CancellationToken cancellation = default);
    Task<IJournalGenericSession> CreateGeneric(CancellationToken cancellationToken = default);
    IReadOnlyDictionary<string, string> GetFieldDescriptions();
    Task<IReadOnlyCollection<string>> GetFields(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<string>> GetFieldValues(string field, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<IReadOnlyDictionary<string, string>>> GetLogs(DateTimeOffset since, string order = "desc", int limit = 100, string? filter = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, int>> GetPriorityCounts(DateTimeOffset since, string? filter = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, int>> GetPriorityCounts(DateTimeOffset start, DateTimeOffset end, string? filter = null, CancellationToken cancellationToken = default);
}
