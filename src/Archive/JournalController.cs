using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ViciOne.Journal;

[Route("api/journal")]
[ApiController]
internal class JournalController(ILogger<JournalController> logger, ILogger<SystemdJournal> loggerJournal, IHostApplicationLifetime appLifetime, IJournalFilterService filterService) : ControllerBase
{
    private const string OrderAscending = "ASC";
    private const string OrderDescending = "DESC";

    private readonly ILogger<JournalController> _logger = logger;
    private readonly ILogger<SystemdJournal> _loggerJournal = loggerJournal;
    private readonly IHostApplicationLifetime _appLifetime = appLifetime;
    private readonly IJournalFilterService _filterService = filterService;

    #region Endpoint 1: Fields

    /// <summary>
    /// GET /api/journal/fields
    /// Gibt alle verfügbaren Journal-Felder mit Beschreibungen zurück.
    /// </summary>
    [HttpGet("fields")]
    public ActionResult<Dictionary<string, string>> GetJournalFields()
    {
#pragma warning disable CA1848 // LoggerMessage-Delegaten verwenden
        _logger.LogInformation("Retrieving journal fields and descriptions.");
#pragma warning restore CA1848 // LoggerMessage-Delegaten verwenden
        try
        {
            return Ok(JournalFieldDescriptions.FieldDescriptions);
        }
        catch (Exception ex)
        {
#pragma warning disable CA1848 // LoggerMessage-Delegaten verwenden
            _logger.LogError(ex, "Error retrieving journal fields.");
#pragma warning restore CA1848 // LoggerMessage-Delegaten verwenden
            return StatusCode(500, "Internal server error retrieving fields.");
        }
    }

    #endregion

    #region Endpoint 2: Unique Field Values

    /// <summary>
    /// GET /api/journal/field-values/{field}  
    /// Gibt die eindeutigen Werte für ein bestimmtes Journal-Feld zurück.
    /// </summary>
    [HttpGet("field-values/{field}")]
    public ActionResult<List<string>> GetFieldValues(string field)
    {
#pragma warning disable CA1848 // LoggerMessage-Delegaten verwenden
        _logger.LogInformation("Retrieving unique values for field '{Field}'.", field);
#pragma warning restore CA1848 // LoggerMessage-Delegaten verwenden
        try
        {
            var values = JournalFieldValuesProvider.GetFieldValues(_loggerJournal, field);
            return Ok(values);
        }
        catch (Exception ex)
        {
#pragma warning disable CA1848 // LoggerMessage-Delegaten verwenden
            _logger.LogError(ex, "Error retrieving unique values for field '{Field}'.", field);
#pragma warning restore CA1848 // LoggerMessage-Delegaten verwenden
            return BadRequest($"Error retrieving field values: {ex.Message}");
        }
    }

    #endregion

    #region Endpoint 3: Journal Logs

    /// <summary>
    /// GET /api/journal/logs  
    /// Liest Journal-Einträge anhand der angegebenen Filter und Parameter.
    /// Unterstützt statische Abfrage oder Live-Streaming via SSE.
    /// Query-Parameter:
    ///   - limit (int, Standard: 100)
    ///   - order ("asc" oder "desc", Standard: "desc")
    ///   - since (ISO 8601 Timestamp, erforderlich)
    ///   - follow (bool, Standard: false)
    ///   - interval (int, Standard: 60 Sekunden)
    ///   - filters (string in journald-Syntax, erforderlich)
    /// </summary>
    [HttpGet("logs")]
    public async Task<IActionResult> GetJournalLogs(CancellationToken cancellationToken)
    {
        try
        {
            // Parameter auswerten und validieren
            var limit = 100;
            if (Request.Query.ContainsKey("limit") &&
                (!int.TryParse(Request.Query["limit"], out limit) || limit <= 0))
            {
                return BadRequest("Parameter 'limit' must be a positive integer.");
            }

            var order = Request.Query.ContainsKey("order")
                           ? Request.Query["order"].ToString().ToUpperInvariant()
                           : OrderDescending;
            if (order is not OrderAscending and not OrderDescending)
            {
                return BadRequest("Parameter 'order' must be either 'asc' or 'desc'.");
            }

            if (!Request.Query.ContainsKey("since"))
            {
                return BadRequest("Parameter 'since' is required.");
            }
#pragma warning disable CS8600 // Das NULL-Literal oder ein möglicher NULL-Wert wird in einen Non-Nullable-Typ konvertiert.
            string sinceStr = Request.Query["since"];
#pragma warning restore CS8600 // Das NULL-Literal oder ein möglicher NULL-Wert wird in einen Non-Nullable-Typ konvertiert.
            if (!DateTime.TryParse(sinceStr, out var since))
            {
                return BadRequest("Invalid 'since' timestamp. Please use ISO 8601 format.");
            }
            if (since > DateTime.UtcNow)
            {
                return BadRequest("Parameter 'since' cannot be in the future.");
            }

            var follow = false;
            if (Request.Query.ContainsKey("follow"))
            {
                if (!bool.TryParse(Request.Query["follow"], out follow))
                {
                    return BadRequest("Parameter 'follow' must be a boolean (true/false).");
                }
            }

            var intervalSec = 60;
            if (Request.Query.ContainsKey("interval") &&
                (!int.TryParse(Request.Query["interval"], out intervalSec) || intervalSec <= 0))
            {
                return BadRequest("Parameter 'interval' must be a positive integer (in seconds).");
            }

            //if (!Request.Query.ContainsKey("filters"))
            //{
            //    return BadRequest("Parameter 'filters' is required.");
            //}
#pragma warning disable CS8600 // Das NULL-Literal oder ein möglicher NULL-Wert wird in einen Non-Nullable-Typ konvertiert.
            string filtersParam = Request.Query["filters"];
#pragma warning restore CS8600 // Das NULL-Literal oder ein möglicher NULL-Wert wird in einen Non-Nullable-Typ konvertiert.

            // Kombiniere den Request-CancellationToken mit dem globalen Application-Stopping-Token
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _appLifetime.ApplicationStopping);
            var ct = cts.Token;

            using var journal = new SystemdJournal(_loggerJournal, JournalOpeningMode.LocalOnly);
            try
            {
                journal.SeekRealtime(since);
            }
            catch (Exception ex)
            {
#pragma warning disable CA1848 // LoggerMessage-Delegaten verwenden
                _logger.LogError(ex, "Error setting journal pointer to {Since}.", since);
#pragma warning restore CA1848 // LoggerMessage-Delegaten verwenden
                return BadRequest($"Error setting journal pointer: {ex.Message}");
            }

            // Filter anwenden über den ausgelagerten Service
            try
            {
                if (!string.IsNullOrEmpty(filtersParam))
                    _filterService.ApplyFilters(journal, filtersParam);
            }
            catch (Exception ex)
            {
#pragma warning disable CA1848 // LoggerMessage-Delegaten verwenden
                _logger.LogError(ex, "Error applying filters: {Filters}", filtersParam);
#pragma warning restore CA1848 // LoggerMessage-Delegaten verwenden
                return BadRequest($"Error in filter parameter: {ex.Message}");
            }

            if (follow)
            {
                // Live-Streaming via SSE
                Response.Headers.Append("Content-Type", "text/event-stream");
                Response.Headers.Append("Cache-Control", "no-cache");

                while (!ct.IsCancellationRequested)
                {
                    if (journal.Next())
                    {
                        var entry = journal.GetCurrentEntry();
                        var json = JsonSerializer.Serialize(entry);
                        await Response.WriteAsync($"data: {json}\n\n", ct);
                        await Response.Body.FlushAsync(ct);
                    }
                    else
                    {
                        await Task.Delay(intervalSec * 1000, ct);
                    }
                }
                return new EmptyResult();
            }
            else
            {
                // Statische Abfrage: Bis zu "limit" Einträge lesen
                var logs = new List<Dictionary<string, string>>(limit);
                var count = 0;
                while (count < limit && journal.Next())
                {
                    logs.Add(journal.GetCurrentEntry());
                    count++;
                }
                if (order == OrderDescending)
                {
                    logs.Reverse();
                }
                return Ok(logs);
            }
        }
        catch (Exception ex)
        {
#pragma warning disable CA1848 // LoggerMessage-Delegaten verwenden
            _logger.LogError(ex, "Unexpected error in GET /logs endpoint.");
#pragma warning restore CA1848 // LoggerMessage-Delegaten verwenden
            return StatusCode(500, "Internal server error in /logs endpoint.");
        }
    }

    #endregion

    #region Endpoint 4: Aggregated Log Priority Count

    /// <summary>
    /// GET /api/journal/priority-count  
    /// Gibt die aggregierte Zählung der Journal-Einträge pro Syslog-Priorität seit dem angegebenen Timestamp zurück.
    /// Unterstützt Live-Streaming (follow=true) mittels SSE.
    /// Query-Parameter:
    ///   - since (ISO 8601 Timestamp, erforderlich)
    ///   - follow (bool, Standard: false)
    ///   - interval (int in Sekunden, Standard: 60)
    /// </summary>
    [HttpGet("priority-count")]
    public async Task<IActionResult> GetPriorityCount(CancellationToken cancellationToken)
    {
        try
        {
            if (!Request.Query.ContainsKey("since"))
            {
                return BadRequest("Parameter 'since' is required.");
            }
#pragma warning disable CS8600 // Das NULL-Literal oder ein möglicher NULL-Wert wird in einen Non-Nullable-Typ konvertiert.
            string sinceStr = Request.Query["since"];
#pragma warning restore CS8600 // Das NULL-Literal oder ein möglicher NULL-Wert wird in einen Non-Nullable-Typ konvertiert.
            if (!DateTime.TryParse(sinceStr, out var since))
            {
                return BadRequest("Invalid 'since' timestamp. Please use ISO 8601 format.");
            }
            if (since > DateTime.UtcNow)
            {
                return BadRequest("Parameter 'since' cannot be in the future.");
            }

            var follow = false;
            if (Request.Query.ContainsKey("follow"))
            {
                if (!bool.TryParse(Request.Query["follow"], out follow))
                {
                    return BadRequest("Parameter 'follow' must be a boolean (true/false).");
                }
            }

            var intervalSec = 60;
            if (Request.Query.ContainsKey("interval") &&
                (!int.TryParse(Request.Query["interval"], out intervalSec) || intervalSec <= 0))
            {
                return BadRequest("Parameter 'interval' must be a positive integer (in seconds).");
            }

            if (follow)
            {
                Response.Headers.Append("Content-Type", "text/event-stream");
                Response.Headers.Append("Cache-Control", "no-cache");

                var previousCounts = new Dictionary<int, int>();

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _appLifetime.ApplicationStopping);
                var ct = cts.Token;

                while (!ct.IsCancellationRequested)
                {
                    using var journal = new SystemdJournal(_loggerJournal, JournalOpeningMode.LocalOnly);
                    try
                    {
                        journal.SeekRealtime(since);
                    }
                    catch (Exception ex)
                    {
#pragma warning disable CA1848 // LoggerMessage-Delegaten verwenden
                        _logger.LogError(ex, "Error setting journal pointer in /priority-count endpoint.");
#pragma warning restore CA1848 // LoggerMessage-Delegaten verwenden
                        await Response.WriteAsync($"data: Error setting journal pointer: {ex.Message}\n\n", ct);
                        break;
                    }
                    var currentCounts = journal.GetPriorityCountsSince(since, cancellationToken);
                    if (!DictionariesEqual(previousCounts, currentCounts))
                    {
                        previousCounts = new Dictionary<int, int>(currentCounts);
                        // Konvertiere die Keys zu Strings für die Ausgabe
                        var outputCounts = currentCounts.ToDictionary(kvp => kvp.Key.ToString(CultureInfo.InvariantCulture), kvp => kvp.Value);
                        var json = JsonSerializer.Serialize(outputCounts);
                        await Response.WriteAsync($"data: {json}\n\n", ct);
                        await Response.Body.FlushAsync(ct);
                    }
                    await Task.Delay(intervalSec * 1000, ct);
                }
                return new EmptyResult();
            }
            else
            {
                using var journal = new SystemdJournal(_loggerJournal, JournalOpeningMode.LocalOnly);
                try
                {
                    journal.SeekRealtime(since);
                }
                catch (Exception ex)
                {
#pragma warning disable CA1848 // LoggerMessage-Delegaten verwenden
                    _logger.LogError(ex, "Error setting journal pointer in /priority-count endpoint.");
#pragma warning restore CA1848 // LoggerMessage-Delegaten verwenden
                    return BadRequest($"Error setting journal pointer: {ex.Message}");
                }
                var counts = journal.GetPriorityCountsSince(since, cancellationToken);
                var outputCounts = counts.ToDictionary(kvp => kvp.Key.ToString(CultureInfo.InvariantCulture), kvp => kvp.Value);
                return Ok(outputCounts);
            }
        }
        catch (Exception ex)
        {
#pragma warning disable CA1848 // LoggerMessage-Delegaten verwenden
            _logger.LogError(ex, "Unexpected error in GET /priority-count endpoint.");
#pragma warning restore CA1848 // LoggerMessage-Delegaten verwenden
            return StatusCode(500, "Internal server error in /priority-count endpoint.");
        }
    }

    #endregion

    #region Endpoint 5: Aggregated Log Count Grouped by Filter

    /// <summary>
    /// POST /api/journal/filter-count  
    /// Zählt die Journal-Einträge, die zu jeder angegebenen Filtergruppe seit dem angegebenen Timestamp passen.
    /// Der Request-Body muss ein JSON-Objekt mit einem "filters"-Array enthalten.
    /// Unterstützt Live-Streaming (follow=true) mittels SSE.
    /// Query-Parameter:
    ///   - since (ISO 8601 Timestamp, erforderlich)
    ///   - follow (bool, Standard: false)
    ///   - interval (int in Sekunden, Standard: 60)
    /// </summary>
    [HttpPost("filter-count")]
    public async Task<IActionResult> GetFilterCount(CancellationToken cancellationToken)
    {
        try
        {
            if (!Request.Query.ContainsKey("since"))
            {
                return BadRequest("Parameter 'since' is required.");
            }
#pragma warning disable CS8600 // Das NULL-Literal oder ein möglicher NULL-Wert wird in einen Non-Nullable-Typ konvertiert.
            string sinceStr = Request.Query["since"];
#pragma warning restore CS8600 // Das NULL-Literal oder ein möglicher NULL-Wert wird in einen Non-Nullable-Typ konvertiert.
            if (!DateTime.TryParse(sinceStr, out var since))
            {
                return BadRequest("Invalid 'since' timestamp. Please use ISO 8601 format.");
            }
            if (since > DateTime.UtcNow)
            {
                return BadRequest("Parameter 'since' cannot be in the future.");
            }

            var follow = false;
            if (Request.Query.ContainsKey("follow"))
            {
                if (!bool.TryParse(Request.Query["follow"], out follow))
                {
                    return BadRequest("Parameter 'follow' must be a boolean (true/false).");
                }
            }

            var intervalSec = 60;
            if (Request.Query.ContainsKey("interval") &&
                (!int.TryParse(Request.Query["interval"], out intervalSec) || intervalSec <= 0))
            {
                return BadRequest("Parameter 'interval' must be a positive integer (in seconds).");
            }

            FilterCountRequest requestBody;
            try
            {
#pragma warning disable CS8600 // Das NULL-Literal oder ein möglicher NULL-Wert wird in einen Non-Nullable-Typ konvertiert.
                requestBody = await JsonSerializer.DeserializeAsync<FilterCountRequest>(Request.Body, cancellationToken: cancellationToken);
#pragma warning restore CS8600 // Das NULL-Literal oder ein möglicher NULL-Wert wird in einen Non-Nullable-Typ konvertiert.
            }
            catch (Exception ex)
            {
#pragma warning disable CA1848 // LoggerMessage-Delegaten verwenden
                _logger.LogError(ex, "Error parsing request body in /filter-count endpoint.");
#pragma warning restore CA1848 // LoggerMessage-Delegaten verwenden
                return BadRequest($"Invalid request body: {ex.Message}");
            }
#pragma warning disable CA1860 // Verwendung der Erweiterungsmethode „Enumerable.Any()“ vermeiden
            if (requestBody == null || requestBody.Filters == null || !requestBody.Filters.Any())
            {
                return BadRequest("Request body must contain a 'filters' array with at least one filter group.");
            }
#pragma warning restore CA1860 // Verwendung der Erweiterungsmethode „Enumerable.Any()“ vermeiden

            if (follow)
            {
                Response.Headers.Append("Content-Type", "text/event-stream");
                Response.Headers.Append("Cache-Control", "no-cache");

                var previousCounts = new Dictionary<string, int>();

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _appLifetime.ApplicationStopping);
                var ct = cts.Token;

                while (!ct.IsCancellationRequested)
                {
                    var currentCounts = new Dictionary<string, int>();
                    foreach (var filterGroup in requestBody.Filters)
                    {
                        try
                        {
                            var count = GetCountForFilterGroup(filterGroup.Filter, since);
                            currentCounts[filterGroup.Name] = count;
                        }
                        catch (Exception ex)
                        {
#pragma warning disable CA1848 // LoggerMessage-Delegaten verwenden
                            _logger.LogError(ex, "Error counting filter group '{FilterName}'.", filterGroup.Name);
#pragma warning restore CA1848 // LoggerMessage-Delegaten verwenden
                            currentCounts[filterGroup.Name] = -1; // Fehlerindikator
                        }
                    }
                    if (!DictionariesEqual(currentCounts, previousCounts))
                    {
                        previousCounts = new Dictionary<string, int>(currentCounts);
                        var json = JsonSerializer.Serialize(currentCounts);
                        await Response.WriteAsync($"data: {json}\n\n", ct);
                        await Response.Body.FlushAsync(ct);
                    }
                    await Task.Delay(intervalSec * 1000, ct);
                }
                return new EmptyResult();
            }
            else
            {
                var resultCounts = new Dictionary<string, int>();
                foreach (var filterGroup in requestBody.Filters)
                {
                    try
                    {
                        var count = GetCountForFilterGroup(filterGroup.Filter, since);
                        resultCounts[filterGroup.Name] = count;
                    }
                    catch (Exception ex)
                    {
#pragma warning disable CA1848 // LoggerMessage-Delegaten verwenden
                        _logger.LogError(ex, "Error counting filter group '{FilterName}'.", filterGroup.Name);
#pragma warning restore CA1848 // LoggerMessage-Delegaten verwenden
                        resultCounts[filterGroup.Name] = -1;
                    }
                }
                return Ok(resultCounts);
            }
        }
        catch (Exception ex)
        {
#pragma warning disable CA1848 // LoggerMessage-Delegaten verwenden
            _logger.LogError(ex, "Unexpected error in POST /filter-count endpoint.");
#pragma warning restore CA1848 // LoggerMessage-Delegaten verwenden
            return StatusCode(500, "Internal server error in /filter-count endpoint.");
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Vergleicht zwei Dictionarys mit int-Keys und int-Values.
    /// </summary>
    private static bool DictionariesEqual(Dictionary<int, int> dict1, Dictionary<int, int> dict2)
    {
        if (dict1.Count != dict2.Count)
            return false;
        foreach (var kvp in dict1)
        {
            if (!dict2.TryGetValue(kvp.Key, out var value) || value != kvp.Value)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Vergleicht zwei Dictionarys mit string-Keys und int-Values.
    /// </summary>
    private static bool DictionariesEqual(Dictionary<string, int> dict1, Dictionary<string, int> dict2)
    {
        if (dict1.Count != dict2.Count)
            return false;
        foreach (var kvp in dict1)
        {
            if (!dict2.TryGetValue(kvp.Key, out var value) || value != kvp.Value)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Ermittelt die Anzahl der Journal-Einträge, die dem angegebenen Filter seit dem gegebenen Timestamp entsprechen.
    /// </summary>
    /// <param name="filterString">Der Filter-String in journald-Syntax.</param>
    /// <param name="since">Der Start-Timestamp.</param>
    /// <returns>Anzahl der passenden Journal-Einträge.</returns>
    private int GetCountForFilterGroup(string filterString, DateTime since)
    {
        var count = 0;
        using var journal = new SystemdJournal(_loggerJournal, JournalOpeningMode.LocalOnly);
        try
        {
            journal.SeekRealtime(since);
        }
        catch (Exception ex)
        {
#pragma warning disable CA1848 // LoggerMessage-Delegaten verwenden
            _logger.LogError(ex, "Error setting journal pointer for filter count.");
#pragma warning restore CA1848 // LoggerMessage-Delegaten verwenden
            throw;
        }
        try
        {
            _filterService.ApplyFilters(journal, filterString);
        }
        catch (Exception ex)
        {
#pragma warning disable CA1848 // LoggerMessage-Delegaten verwenden
            _logger.LogError(ex, "Error applying filter for count: {FilterString}", filterString);
#pragma warning restore CA1848 // LoggerMessage-Delegaten verwenden
            throw;
        }
        while (journal.Next())
        {
            count++;
        }
        return count;
    }

    #endregion
}
