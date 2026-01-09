using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace ViciOne.Journal;

internal static partial class JournalFilterService
{
    internal static void ApplyFilters(SystemdJournal journal, string filters, ILogger logger)
    {
        try
        {
            // Zerlege den gesamten Filter-String in einzelne Filterausdrücke (jeder beginnt mit einem Feldnamen)
            var filterExpressions = FilterSplitRegex().Split(filters);
            foreach (var expression in filterExpressions)
            {
                var exprTrim = expression.Trim();
                if (string.IsNullOrWhiteSpace(exprTrim))
                    continue;
                var equalIndex = exprTrim.IndexOf('=', StringComparison.Ordinal);
                if (equalIndex == -1)
                {
                    throw new ArgumentException($"Ungültiger Filterausdruck: '{exprTrim}'. Erwartetes Format: 'FELD=Kriterien'.");
                }
                var field = exprTrim[..equalIndex].Trim();
                var criteria = exprTrim[(equalIndex + 1)..].Trim();
                if (string.IsNullOrEmpty(field) || string.IsNullOrEmpty(criteria))
                {
                    throw new ArgumentException($"Ungültiger Filterausdruck: '{exprTrim}'. Feld und Kriterien müssen gesetzt sein.");
                }
                // Wende die Filter für dieses Feld an
                ApplyFilter(journal, field, criteria.AsSpan());
            }
        }
        catch (Exception ex)
        {
#pragma warning disable CA1848 // LoggerMessage-Delegaten verwenden
            logger.LogError(ex, "Fehler beim Anwenden der Filter: {Filters}", filters);
#pragma warning restore CA1848 // LoggerMessage-Delegaten verwenden
            throw;
        }
    }

    /// <summary>
    /// Wendet für ein einzelnes Feld einen Filter an, der in OR‑Gruppen (durch Komma getrennt)
    /// und innerhalb jeder Gruppe in AND‑Bedingungen (durch '+' getrennt) unterteilt ist.
    /// Für jede AND-Bedingung wird journal.AddMatch() aufgerufen, und zwischen OR-Gruppen
    /// wird journal.AddDisjunction() gesetzt.
    /// </summary>
    /// <param name="journal">Die Journal-Instanz.</param>
    /// <param name="field">Das zu filternde Feld.</param>
    /// <param name="valueSpan">Der Filter-String (ohne Feldnamen), z. B. "sshd+root" oder "sshd,nginx".</param>
    internal static void ApplyFilter(SystemdJournal journal, string field, ReadOnlySpan<char> valueSpan)
    {
        var start = 0;
        var firstGroup = true;
        while (start < valueSpan.Length)
        {
            var commaIndex = valueSpan[start..].IndexOf(',');
            var orGroup = commaIndex == -1
                ? valueSpan[start..]
                : valueSpan.Slice(start, commaIndex);
            orGroup = orGroup.Trim();
            if (!orGroup.IsEmpty)
            {
                if (!firstGroup)
                {
                    // Zwischen OR-Gruppen einen Disjunction-Marker setzen, damit das Ergebnis
                    // als (Group1) OR (Group2) interpretiert wird.
                    journal.AddDisjunction();
                }
                var innerStart = 0;
                // Teile die OR-Gruppe in AND-Bedingungen auf
                while (innerStart < orGroup.Length)
                {
                    var plusIndex = orGroup[innerStart..].IndexOf('+');
                    var part = plusIndex == -1
                        ? orGroup[innerStart..]
                        : orGroup.Slice(innerStart, plusIndex);
                    part = part.Trim();
                    if (!part.IsEmpty)
                    {
                        // Jede AND-Bedingung wird separat hinzugefügt. Mehrere AddMatch-Aufrufe
                        // für dasselbe Feld werden logisch AND verknüpft.
                        journal.AddMatch(field, part.ToString());
                    }
                    innerStart += plusIndex == -1 ? orGroup.Length - innerStart : plusIndex + 1;
                }
                firstGroup = false;
            }
            start += commaIndex == -1 ? valueSpan.Length - start : commaIndex + 1;
        }
    }

    [GeneratedRegex(",(?=[A-Z0-9_]+=)", RegexOptions.Compiled)]
    private static partial Regex FilterSplitRegex();
}
