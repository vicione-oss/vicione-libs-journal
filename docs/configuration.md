# Configuration Reference

This document provides a complete reference for all configuration options available in ViciOne.Journal.

## Table of Contents

- [JournalOptions](#journaloptions)
- [JournalLoggerOptions](#journalloggeroptions)
- [JournalSpamGuardOptions](#journalspamguardoptions)

---

## JournalOptions

`JournalOptions` configures the behavior of the `JournalService` for reading journal entries.

### Properties

| Property        | Type                 | Default     | Description                                                |
| --------------- | -------------------- | ----------- | ---------------------------------------------------------- |
| `OpeningMode`   | `JournalOpeningMode` | `LocalOnly` | Specifies which journal files to open                      |
| `EnableLogging` | `bool`               | `false`     | Enables internal diagnostic logging for journal operations |
| `MaxSessions`   | `int`                | `5`         | Generic and follow sessions that may be open at once       |
| `MaxConcurrentQueries` | `int`         | `4`         | One-shot queries (`GetLogs`, `GetPriorityHistogram`, ...) that may run at once |

When a limit is reached, the call fails at once with a `JournalBusyException` (an `InvalidOperationException`) instead of waiting. Sessions and queries have separate limits, so open sessions never block a query.

### JournalOpeningMode Enum

The `JournalOpeningMode` enum controls which journal files are opened for reading. These flags can be combined using bitwise OR operations.

| Mode          | Value | Description                                                    |
| ------------- | ----- | -------------------------------------------------------------- |
| `None`        | 0     | Opens all accessible journals (system + user)                  |
| `LocalOnly`   | 1     | Only local machine journals (excludes remote/network journals) |
| `RuntimeOnly` | 2     | Only in-memory journals (excludes persistent storage)          |
| `System`      | 4     | Only system journal (excludes user journals)                   |
| `CurrentUser` | 8     | Only the current user's journal                                |

---

## JournalLoggerOptions

`JournalLoggerOptions` configures the `JournalLogger` for writing log entries to the systemd journal.

### Properties

| Property               | Type                          | Default     | Description                                  |
| ---------------------- | ----------------------------- | ----------- | -------------------------------------------- |
| `OpenMode`             | `JournalOpeningMode`          | `LocalOnly` | Journal opening mode for the writer          |
| `OnError`              | `Action<string>`              | `Debug.Fail(...)` | Callback invoked when a logging error occurs |
| `EntryPrefix`          | `string`                      | `""`        | Prefix added to the SYSLOG_IDENTIFIER field  |
| `MetaDataFields`       | `Dictionary<string, object?>` | Empty       | Additional fields added to every log entry   |
| `WriteStructuredData`  | `bool`                        | `true`      | Include structured data from log templates   |
| `WriteCustomKeyValues` | `bool`                        | `false`     | Write custom key-value pairs from log state  |
| `SpamGuard`            | `JournalSpamGuardOptions`     | Default     | Spam protection configuration                |

With `WriteCustomKeyValues`, a key-value whose field name is reserved by the sink or journald (`MESSAGE`, `PRIORITY`, `SYSLOG_STRUCTURED_DATA`, `SYSLOG_IDENTIFIER`, `SYSLOG_FACILITY`, `MESSAGE_ID`, `CODE_FILE`, `ERRNO`, `UNIT`, ...) is written with a `PROP_` prefix, for example `{Message}` as `PROP_MESSAGE`. `MetaDataFields` are written as configured, so they can still set fields such as `SYSLOG_IDENTIFIER`.

---

## JournalSpamGuardOptions

`JournalSpamGuardOptions` configures the spam guard feature that prevents log flooding by aggregating repeated messages.

### Properties

| Property                 | Type            | Default            | Description                                    |
| ------------------------ | --------------- | ------------------ | ---------------------------------------------- |
| `Enabled`                | `bool`          | `false`            | Enable/disable spam guard                      |
| `Window`                 | `TimeSpan`      | 5 seconds          | Time window for aggregating duplicate messages |
| `FlushInterval`          | `TimeSpan`      | 250 ms             | How often aggregated summaries are flushed     |
| `SummaryCountThreshold`  | `int`           | 100                | Minimum count before creating a summary        |
| `MaxBucketCapacity`      | `int`           | 512                | Maximum number of unique messages to track     |
| `SummaryMessageTemplate` | `string`        | See below          | Template for summary messages                  |
| `SummaryCountFieldName`  | `string`        | `"SPAM_COUNT"`     | Field name for repeat count                    |
| `SummaryWindowFieldName` | `string`        | `"SPAM_WINDOW_MS"` | Field name for window duration                 |
| `AggregationFields`      | `IList<string>` | Empty              | Fields used to identify duplicate messages     |
| `TimeProvider`           | `TimeProvider`  | `System`           | Time provider (for testing)                    |

---

## See Also

- [Getting Started](getting-started.md) - Basic usage guide
- [Troubleshooting](troubleshooting.md) - Common issues and solutions
