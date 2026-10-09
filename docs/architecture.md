# Architecture Overview

This document describes the architecture of ViciOne.Journal.

## Package Structure

ViciOne.Journal consists of three NuGet packages:

| Package                     | Purpose                                  |
| --------------------------- | ---------------------------------------- |
| **ViciOne.Journal**         | Core library for reading journal entries |
| **ViciOne.Journal.Logging** | Microsoft.Extensions.Logging integration |
| **Serilog.Sinks.Journal**   | Serilog sink for journal writing         |

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Your Application                     │
└─────────────────────────────────────────────────────────┘
                          │
          ┌───────────────┼───────────────┐
          ▼               ▼               ▼
┌─────────────────┐ ┌───────────┐ ┌───────────────┐
│ JournalService  │ │ ILogger   │ │ Serilog       │
│   (Reading)     │ │ (Writing) │ │   (Writing)   │
└─────────────────┘ └───────────┘ └───────────────┘
          │               │               │
          └───────────────┼───────────────┘
                          ▼
┌─────────────────────────────────────────────────────────┐
│                   Native Interop                        │
│              (P/Invoke to libsystemd)                   │
└─────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────┐
│                  libsystemd.so.0                        │
└─────────────────────────────────────────────────────────┘
```

## Key Components

### Reading (ViciOne.Journal)

- **JournalService** - Main service for querying logs
- **JournalGenericSession** - Flexible session with full seek/filter control
- **JournalFollowSession** - Real-time log following (like `tail -f`)
- **JournalLookBehindSession** - Optimized for reading historical logs

### Writing (ViciOne.Journal.Logging)

- **JournalLoggerProvider** - Creates logger instances
- **JournalLogger** - ILogger implementation
- **JournalSpamAggregator** - Prevents log flooding
- **SystemdJournalWriter** - Writes entries via native API

## Native Interop

The library uses P/Invoke to call the systemd journal C API:

```csharp
[LibraryImport("libsystemd.so.0")]
internal static partial int sd_journal_open(out IntPtr journal, int flags);

[LibraryImport("libsystemd.so.0")]
internal static partial int sd_journal_next(IntPtr journal);

[LibraryImport("libsystemd.so.0")]
internal static partial void sd_journal_close(IntPtr journal);
```

A `SafeJournalHandle` ensures proper cleanup of native resources.

## Thread Safety

- **AccessControl** limits open sessions (`MaxSessions`, default 5) and running queries (`MaxConcurrentQueries`, default 4) separately and fails fast with a `JournalBusyException`
- Each session has its own journal handle
- One-shot queries run on a pool of journals that stay open between queries, each on its own thread, because opening a journal maps all of its files
- Sessions should not be shared across threads

## Memory Management

Most journal API functions return pointers into the journal's own memory, valid only until the next call on the same journal, that must **not** be freed. The exception is `sd_journal_get_cursor`, which returns a string allocated with `malloc()` that the caller frees.

## Logging Pipeline

```
ILogger.Log() → JournalLogger → SpamAggregator? → Channel<T> → Writer → Native API
```

- Logs are queued via `Channel<T>` for non-blocking writes
- A background task processes the queue
- Optional spam guard aggregates duplicate messages

## See Also

- [Configuration](configuration.md) - Configuration options
- [Troubleshooting](troubleshooting.md) - Common issues
