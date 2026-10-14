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
| `Directory`     | `string?`            | `null`      | Reads the journal files of this directory instead, such as a copy from another device; `OpeningMode` does not apply |
| `MaxSessions`   | `int`                | `5`         | Generic and follow sessions that may be open at once       |
| `MaxConcurrentQueries` | `int`         | `4`         | One-shot queries (`GetLogs`, `GetPriorityHistogram`, ...) that may run at once; each keeps its journal open for the next query |

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

### Aggregation strategies

The spam guard groups repeated entries into a single summary. The only thing that
differs between strategies is which fields form the aggregation key; bucketing,
windowing, threshold counting, and summary emission are shared and unchanged.

| Strategy                    | Key fields                              | Best for                                             |
| --------------------------- | --------------------------------------- | ---------------------------------------------------- |
| Message-based (default)     | `MESSAGE`, `CATEGORY`, `PRIORITY`, `EXCEPTION` | Repeated identical messages                    |
| EventId-based (opt-in)      | `EVENT_ID`, `CATEGORY`                   | Floods with unique text but a stable call site       |
| Template-based (opt-in)     | `MESSAGE_TEMPLATE`                       | Serilog floods with unique text but a stable call site |

When `AggregationFields` is empty, message-based aggregation is used
(`MESSAGE`, `CATEGORY`, `PRIORITY`, `EXCEPTION`). This is the default and remains
unchanged.

#### Message-based (default)

Keys on the rendered message text. Ideal when the same message repeats verbatim:

```csharp
options.SpamGuard.Enabled = true;
// No AggregationFields set → message-based aggregation.
```

This cannot collapse messages that embed a varying value, because every rendered
message is unique:

```csharp
logger.LogWarning("Request {RequestId} failed", requestId);
// "Request 1001 failed", "Request 1002 failed", ... → all unique → never aggregated
```

#### EventId-based (opt-in)

Keys on the `EVENT_ID` and `CATEGORY` fields, which are stable per call site and
ignore the rendered text. This collapses the flood above:

```csharp
options.SpamGuard.Enabled = true;
options.SpamGuard.AggregationFields.Clear();
options.SpamGuard.AggregationFields.Add("EVENT_ID");
options.SpamGuard.AggregationFields.Add("CATEGORY");
```

For this to work, the log call must supply an `EventId`:

```csharp
logger.LogWarning(new EventId(1001, "RequestFailed"), "Request {RequestId} failed", requestId);
```

Calls that do not are forwarded unaggregated rather than collapsed - this
strategy does nothing for them. See
[Missing aggregation fields](#missing-aggregation-fields).

`JournalLogger` surfaces the `EventId` as the `EVENT_ID` entry field. When the
`EventId` has a name, the field is `"{Id}:{Name}"` (e.g. `1001:RequestFailed`);
otherwise it is the numeric id (e.g. `1001`).

`EVENT_ID` and `CATEGORY` are reserved: a log template placeholder that happens
to share one of these names (e.g. `logger.LogInformation("{EventId} occurred", businessEventId)`)
is dropped rather than being written under the reserved key, so it cannot be
mistaken for the real `EventId`/category.

`EVENT_ID` is a new field. It is written to an entry - as a bare top-level
field, never inside `SYSLOG_STRUCTURED_DATA` - when `WriteCustomKeyValues` is
`true`, or when spam guard is enabled *and* `EVENT_ID` is part of the resolved
`AggregationFields` for the active strategy. If spam guard is disabled, or is
aggregating on a different set of fields, it is only written when
`WriteCustomKeyValues` is `true` - a consumer aggregating on `CATEGORY` alone,
for example, gains no `EVENT_ID` field even with spam guard enabled.

`CATEGORY` pre-dates this feature and its behavior is unchanged: it is always
written into `SYSLOG_STRUCTURED_DATA` when `WriteStructuredData` is `true`,
and to the top level when `WriteCustomKeyValues` is `true`, regardless of spam
guard configuration. It is *not* forced to the top level for spam guard's
sake. This means the `EVENT_ID` + `CATEGORY` combination above only
discriminates by category if `WriteCustomKeyValues` is also `true` - with
`WriteCustomKeyValues` `false` (the default), `CATEGORY` is absent from every
entry's top level. Since `CATEGORY` was named explicitly, its absence means
those entries are forwarded unaggregated rather than bucketed on whatever
fields remain - see [Missing aggregation fields](#missing-aggregation-fields).

#### Template-based (Serilog, opt-in)

Serilog has no `EventId` concept. Its stable per call site key is instead the raw
message template, which the Serilog sink surfaces as the `MESSAGE_TEMPLATE` entry
field. Keying on it collapses a flood of unique rendered messages:

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Journal(
        configureSpamGuard: spam => spam.AggregateByMessageTemplate())
    .CreateLogger();

Log.Warning("Request {RequestId} failed", requestId);
// "Request 1001 failed", "Request 1002 failed", ... → same MESSAGE_TEMPLATE → collapsed
```

`MESSAGE_TEMPLATE` is a reserved journal field. It is written to an entry - as
a bare top-level field, never inside `SYSLOG_STRUCTURED_DATA` - regardless of
`writeCustomKeyValues`, but only while spam guard is enabled *and*
`MESSAGE_TEMPLATE` is part of the resolved `AggregationFields` for the active
strategy. Otherwise it is only written when `writeCustomKeyValues` is `true`,
same as any other custom field.

The template (`"Request {RequestId} failed"`) is identical regardless of the
rendered values, so all entries from the same call site share a bucket.

#### Combining strategies

Both key sources can be combined. All configured fields must match for entries to
collapse:

```csharp
options.SpamGuard.AggregationFields.Clear();
options.SpamGuard.AggregationFields.Add("MESSAGE");
options.SpamGuard.AggregationFields.Add("EVENT_ID");
options.SpamGuard.AggregationFields.Add("CATEGORY");
```

#### Missing aggregation fields

**An entry that does not carry every explicitly configured aggregation field is
forwarded unaggregated.** It is never bucketed on the fields that remain.

This matters because an absent field is not a value - it would canonicalize to
the same `null` on every entry that lacks it, so *all* of them would share one
bucket and be suppressed as if they were repeats of each other. The spam guard
fails open instead: losing a deduplication opportunity is acceptable, losing log
lines is not.

The common way to reach this is the default `EventId`. It is `0` with no name on
every `ILogger` overload that does not take one, and `JournalLogger` omits the
`EVENT_ID` field for that case. So with EventId-based aggregation configured:

```csharp
logger.LogWarning("Request {RequestId} failed", requestId);   // no EventId
// → no EVENT_ID field → forwarded unaggregated, every time
```

**The consequence is that EventId-based aggregation provides no spam protection
at all for call sites without an explicit `EventId`.** Nothing is lost, but
nothing is collapsed either. To actually get the benefit, give every call site a
distinct id. The `[LoggerMessage]` source generator is the reliable way to do
this - since .NET 8 it derives a stable id per method from the method name when
`EventId` is omitted, so ids cannot silently go missing:

```csharp
[LoggerMessage(Level = LogLevel.Warning, Message = "Request {RequestId} failed")]
public static partial void RequestFailed(ILogger logger, int requestId);
```

This applies to any explicitly configured field, not just `EVENT_ID` - the same
holds for `CATEGORY` without `WriteCustomKeyValues`, or `MESSAGE_TEMPLATE` on an
entry that has none. A field whose value is present but empty counts as missing
too; neither discriminates.

The built-in default field set (`MESSAGE`, `CATEGORY`, `PRIORITY`, `EXCEPTION`)
is exempt from this check and always aggregates. It deliberately contains fields
that are normally absent, and relies on `MESSAGE` and `PRIORITY` - which are
written to every entry - to discriminate. Message-based aggregation therefore
behaves exactly as before.

> One consequence of that exemption: listing the default fields *explicitly*
> is not the same as leaving `AggregationFields` empty. `EXCEPTION` in
> particular is never written to an entry, so naming it explicitly means no
> entry ever satisfies the field set and nothing is aggregated. Leave
> `AggregationFields` empty to get the default strategy.

---

## See Also

- [Getting Started](getting-started.md) - Basic usage guide
- [Troubleshooting](troubleshooting.md) - Common issues and solutions
