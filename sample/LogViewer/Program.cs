using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using ViciOne.Journal;

using NullLoggerFactory loggerFactory = new();
var options = Substitute.For<IOptions<JournalOptions>>();
options.Value.Returns(new JournalOptions { OpeningMode = JournalOpeningMode.LocalOnly });
using JournalService service = new(loggerFactory, options);
using var session = await service.CreateGeneric().ToLookBehind();
if (args.Length == 1)
    session.SetFilter(args[0]);

const string TimestampFieldName = "REALTIME_TIMESTAMP";
const string HostnameFieldName = "_HOSTNAME";
const string SyslogIdentifierFieldName = "SYSLOG_IDENTIFIER";
const string MessageFieldName = "MESSAGE";
const string PidFieldName = "_PID";
const string PriorityFieldName = "PRIORITY";

while (true)
{
    foreach (var entry in session.GetLogs(4))
    {
        if (entry.TryGetValue(TimestampFieldName, out var rawTimestamp) &&
            DateTimeOffset.TryParse(rawTimestamp, CultureInfo.InvariantCulture, out var timestamp) &&
            entry.TryGetValue(MessageFieldName, out var message) &&
            entry.TryGetValue(SyslogIdentifierFieldName, out var syslogIdentifier) &&
            entry.TryGetValue(HostnameFieldName, out var hostname))
        {
            StringBuilder line = new();
            line.Append(CultureInfo.InvariantCulture, $"{timestamp:MMM dd HH:mm:ss} {hostname}");

            if (entry.TryGetValue(PidFieldName, out var pid))
                line.Append(CultureInfo.InvariantCulture, $" {syslogIdentifier}[{pid}]: ");
            else
                line.Append(CultureInfo.InvariantCulture, $" {syslogIdentifier}: ");

            if (entry.TryGetValue(PriorityFieldName, out var priority))
                line.Append(ApplyColorFormatting(message, priority));
            else
                line.Append(message);

            Console.WriteLine(line);
        }
    }

    Console.ReadKey();
}

static string ApplyColorFormatting(string logEntry, string priority)
    => priority switch
    {
        "0" => $"\u001b[31m{logEntry}\u001b[0m", // EMERG - Red
        "1" => $"\u001b[31m{logEntry}\u001b[0m", // ALERT - Red
        "2" => $"\u001b[31m{logEntry}\u001b[0m", // CRIT - Red
        "3" => $"\u001b[31m{logEntry}\u001b[0m", // ERR - Red
        "4" => $"\u001b[33m{logEntry}\u001b[0m", // WARNING - Yellow
        "5" => $"\u001b[32m{logEntry}\u001b[0m", // NOTICE - Green
        "6" => $"\u001b[34m{logEntry}\u001b[0m", // INFO - Blue
        "7" => $"\u001b[37m{logEntry}\u001b[0m", // DEBUG - White
        _ => logEntry // Default - No color
    };
