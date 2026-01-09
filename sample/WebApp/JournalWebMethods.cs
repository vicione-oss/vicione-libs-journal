using Microsoft.AspNetCore.Mvc;

namespace ViciOne.Journal;

internal static class JournalWebMethods
{
    public static IResult GetFieldDescriptions([FromServices] JournalService journal)
    {
        try
        {
            return Results.Ok(journal.GetFieldDescriptions());
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    public static async Task<IResult> GetFields([FromServices] JournalService journal, CancellationToken cancellationToken)
    {
        try
        {
            using CancellationTokenSource timeoutTokenSource = new(TimeSpan.FromSeconds(3));
            using var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, cancellationToken);

            return Results.Ok(await journal.GetFields(combinedTokenSource.Token));
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    public static async Task<IResult> GetFieldValues([FromServices] JournalService journal, [FromRoute] string field, CancellationToken cancellationToken)
    {
        try
        {
            using CancellationTokenSource timeoutTokenSource = new(TimeSpan.FromSeconds(3));
            using var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, cancellationToken);

            return Results.Ok(await journal.GetFieldValues(field, combinedTokenSource.Token));
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    public static async Task<IResult> GetLogs([FromServices] JournalService journal, [FromQuery] DateTimeOffset since,
        [FromQuery] string order = "desc", [FromQuery] int limit = 100, [FromQuery] string filter = "", CancellationToken cancellationToken = default)
    {
        try
        {
            using CancellationTokenSource timeoutTokenSource = new(TimeSpan.FromSeconds(3));
            using var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, cancellationToken);

            return Results.Ok(await journal.GetLogs(since, order, limit, filter, combinedTokenSource.Token));
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    public static async Task<IResult> GetPriorityCounts([FromServices] JournalService journal, [FromQuery] DateTimeOffset? since = null, [FromQuery] DateTimeOffset? start = null, [FromQuery] DateTimeOffset? end = null, CancellationToken cancellationToken = default)
    {
        if (since.HasValue && !start.HasValue && !end.HasValue)
        {
            try
            {
                return Results.Ok(await journal.GetPriorityCounts(since.Value, cancellationToken: cancellationToken));
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        }
        else if (!since.HasValue && start.HasValue && end.HasValue)
        {
            try
            {
                return Results.Ok(await journal.GetPriorityCounts(start.Value, end.Value, cancellationToken: cancellationToken));
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        }
        else
        {
            return Results.Problem("Either 'since' or 'start' and 'end' must be provided.");
        }
    }
}
