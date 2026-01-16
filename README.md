# ViciOne.Journal

**ViciOne.Journal** is a .NET library that provides a managed wrapper around the Linux systemd journal (sd-journal) C API. It enables .NET applications to read, write, filter, and follow systemd journal logs with ease.

## 📦 NuGet Packages

| Package                     | Description                                                     |
| --------------------------- | --------------------------------------------------------------- |
| **ViciOne.Journal**         | Core library for reading and querying systemd journal entries   |
| **ViciOne.Journal.Logging** | Microsoft.Extensions.Logging integration for writing to journal |
| **Serilog.Sinks.Journal**   | Serilog sink for writing to journal                             |

## ✨ Key Features

- **Read & Query** - Read and query systemd journal entries with powerful filtering
- **Write Logs** - Write logs to systemd journal via ILogger or Serilog
- **Real-time Following** - Follow journal entries in real-time (tail -f style)
- **Historical Retrieval** - Look-behind sessions for historical log retrieval
- **Spam Protection** - Spam guard/aggregation to prevent log flooding
- **Structured Data** - Support for structured data and custom key-value fields
- **Configurable Modes** - Support for different journal opening modes

## 📚 Documentation

| Document                                   | Description                        |
| ------------------------------------------ | ---------------------------------- |
| [Getting Started](docs/getting-started.md) | Installation and basic usage guide |
| [Configuration](docs/configuration.md)     | Complete configuration reference   |
| [Filtering](docs/filtering.md)             | Guide to filtering journal entries |
| [Architecture](docs/architecture.md)       | Library architecture and design    |
| [Troubleshooting](docs/troubleshooting.md) | Common issues and solutions        |

## 🚀 Quick Start

### Reading Journal Entries

```csharp
using ViciOne.Journal;

// Configure services
builder.Services
    .AddSingleton<JournalService>()
    .AddOptions<JournalOptions>();

// Query logs
var service = serviceProvider.GetRequiredService<JournalService>();
var logs = await service.GetLogs(
    since: DateTimeOffset.Now.AddHours(-1),
    order: "desc",
    limit: 100,
    filter: "PRIORITY=3");
```

### Writing with Microsoft.Extensions.Logging

```csharp
using ViciOne.Journal;

builder.Logging.AddJournal(options =>
{
    options.EntryPrefix = "MyApp";
    options.WriteStructuredData = true;
    options.MetaDataFields["APP_VERSION"] = "1.0.0";
});
```

### Writing with Serilog

```csharp
using Serilog;
using Serilog.Sinks.Journal;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Journal(
        metaDataFields: new Dictionary<string, object?> { ["APP"] = "MyApp" },
        writeStructuredData: true)
    .CreateLogger();
```

## 📋 Requirements

- **Operating System**: Linux with systemd
- **.NET Version**: .NET 9.0 or later
- **Native Library**: libsystemd.so.0

### Development Setup

1. Clone the repository
2. Ensure you have .NET 9.0 SDK installed
3. Open the solution in your IDE
4. Run the tests: `dotnet test`

### Code Guidelines

- Follow C# coding conventions
- Include XML documentation for public APIs
- Add unit tests for new functionality
- Update documentation as needed
