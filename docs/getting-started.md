# Getting Started with ViciOne.Journal

This guide will help you get started with ViciOne.Journal, covering installation, basic configuration, and common usage patterns.

## Prerequisites

Before using ViciOne.Journal, ensure you have:

- **Linux operating system** with systemd
- **.NET 9.0 SDK** or later
- **libsystemd.so.0** native library (usually pre-installed on systemd-based distributions)

> ⚠️ **Note**: ViciOne.Journal only works on Linux systems with systemd. It uses P/Invoke to call the native `libsystemd` library.

### Verify systemd is available

```bash
# Check if systemd is running
systemctl --version

# Check if libsystemd is available
ldconfig -p | grep libsystemd
```

## Installation

ViciOne.Journal is distributed as three separate NuGet packages. Install the package(s) you need:

### Core Library (Reading Journal Entries)

```bash
dotnet add package ViciOne.Journal
```

### Microsoft.Extensions.Logging Integration (Writing Logs)

```bash
dotnet add package ViciOne.Journal.Logging
```

### Serilog Sink (Writing Logs with Serilog)

```bash
dotnet add package Serilog.Sinks.Journal
```

## Basic Usage

### Reading Journal Entries with JournalService

The `JournalService` class is the primary way to read and query systemd journal entries.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ViciOne.Journal;

var builder = Host.CreateApplicationBuilder(args);

// Register JournalService with DI
builder.Services
    .AddSingleton<JournalService>()
    .AddOptions<JournalOptions>();

var app = builder.Build();

// Get the service
var journalService = app.Services.GetRequiredService<JournalService>();

// Query the last hour of logs
var logs = await journalService.GetLogs(
    since: DateTimeOffset.Now.AddHours(-1),
    order: "desc",
    limit: 100);

foreach (var entry in logs)
{
    if (entry.TryGetValue("MESSAGE", out var message))
    {
        Console.WriteLine(message);
    }
}
```

### Writing Logs with Microsoft.Extensions.Logging

The `ViciOne.Journal.Logging` package provides integration with `Microsoft.Extensions.Logging`.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ViciOne.Journal;

var builder = Host.CreateApplicationBuilder(args);

// Add Journal logger
builder.Logging.AddJournal(options =>
{
    options.EntryPrefix = "MyApp";
    options.WriteStructuredData = true;
    options.MetaDataFields["APP_VERSION"] = "1.0.0";
});

var app = builder.Build();

// Use the logger
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Application started");
logger.LogWarning("This is a warning with {Parameter}", "value");
logger.LogError("An error occurred!");
```

### Writing Logs with Serilog

The `Serilog.Sinks.Journal` package provides a Serilog sink for writing to the systemd journal.

```csharp
using Serilog;
using Serilog.Sinks.Journal;

// Configure Serilog with Journal sink
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Journal(
        metaDataFields: new Dictionary<string, object?>
        {
            ["APP_NAME"] = "MyApp",
            ["APP_VERSION"] = "1.0.0"
        },
        writeStructuredData: true)
    .CreateLogger();

try
{
    Log.Information("Application starting up");
    Log.Debug("Debug information: {DebugData}", new { Key = "Value" });
    
    // Your application code here
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
```

## Minimal Working Example

Here's a complete, minimal example that demonstrates reading and writing to the journal:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ViciOne.Journal;

var builder = Host.CreateApplicationBuilder(args);

// Configure journal reading
builder.Services
    .AddSingleton<JournalService>()
    .AddOptions<JournalOptions>();

// Configure journal writing
builder.Logging.AddJournal(options =>
{
    options.EntryPrefix = "JournalDemo";
    options.WriteStructuredData = true;
});

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
var journalService = app.Services.GetRequiredService<JournalService>();

// Write a log entry
logger.LogInformation("Hello from ViciOne.Journal!");

// Wait a moment for the entry to be written
await Task.Delay(100);

// Read recent entries from our application
var logs = await journalService.GetLogs(
    since: DateTimeOffset.Now.AddMinutes(-1),
    order: "desc",
    limit: 10,
    filter: "SYSLOG_IDENTIFIER=JournalDemo");

Console.WriteLine($"Found {logs.Count} recent log entries:");
foreach (var entry in logs)
{
    if (entry.TryGetValue("MESSAGE", out var message) &&
        entry.TryGetValue("REALTIME_TIMESTAMP", out var timestamp))
    {
        Console.WriteLine($"[{timestamp}] {message}");
    }
}
```

## Following Journal Entries in Real-Time

ViciOne.Journal supports following journal entries in real-time, similar to `journalctl -f`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ViciOne.Journal;

var builder = Host.CreateApplicationBuilder(args);

// Register JournalService with DI
builder.Services
    .AddSingleton<JournalService>()
    .AddOptions<JournalOptions>();

var app = builder.Build();

var journalService = app.Services.GetRequiredService<JournalService>();

// Create a follow session
using var follower = await journalService.CreateFollower(TimeSpan.FromSeconds(1));

// Subscribe to new entries
follower.NewEntry += entry =>
{
    if (entry.TryGetValue("MESSAGE", out var message))
    {
        Console.WriteLine($"New entry: {message}");
    }
};

// Optionally set a filter
follower.SetFilter("PRIORITY=6"); // INFO level only

// Start following (shows last 10 entries first, then new ones)
follower.Start(logCount: 10);

Console.WriteLine("Press any key to stop following...");
Console.ReadKey();

await follower.Stop();
```

## Historical Log Retrieval with LookBehind Session

For retrieving historical logs in batches:

```csharp
using ViciOne.Journal;

// Create a look-behind session
using var lookBehind = await journalService.CreateGeneric().ToLookBehind();

// Optionally set a filter
lookBehind.SetFilter("PRIORITY=3"); // Errors only

// Get logs in batches
foreach (var entry in lookBehind.GetLogs(size: 50))
{
    if (entry.TryGetValue("MESSAGE", out var message))
    {
        Console.WriteLine(message);
    }
}
```

## Next Steps

- [Configuration Reference](configuration.md) - Learn about all configuration options
- [Filtering Guide](filtering.md) - Learn how to filter journal entries
- [Troubleshooting](troubleshooting.md) - Common issues and solutions
