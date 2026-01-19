# Filtering Guide

This guide explains how to filter journal entries in ViciOne.Journal using the systemd journal filter syntax.

## Table of Contents

- [Filter Syntax](#filter-syntax)
- [Basic Filtering](#basic-filtering)
- [Combining Filters](#combining-filters)
- [Common Filter Patterns](#common-filter-patterns)
- [Available Journal Fields](#available-journal-fields)
- [Filter Examples](#filter-examples)

---

## Filter Syntax

ViciOne.Journal uses the standard systemd journal filter syntax based on field-value matching.

### Basic Format

```
FIELD=value
```

- **FIELD**: Must be uppercase letters, digits, and underscores only
- **=**: Separator between field and value
- **value**: The value to match (case-sensitive)

### Rules

1. Field names must match the regex pattern: `^[A-Z0-9_]+$`
2. Field names cannot start with `_` when adding custom fields (underscore prefix is reserved for trusted journal fields)
3. Values containing spaces must be enclosed in quotes
4. Filters are case-sensitive

---

## Basic Filtering

### Single Field Match

Filter entries where a field exactly matches a value:

```csharp
// Filter by priority (errors only)
var logs = await journalService.GetLogs(
    since: DateTimeOffset.Now.AddHours(-1),
    filter: "PRIORITY=3");

// Filter by syslog identifier
var logs = await journalService.GetLogs(
    since: DateTimeOffset.Now.AddHours(-1),
    filter: "SYSLOG_IDENTIFIER=nginx");

// Filter by process ID
var logs = await journalService.GetLogs(
    since: DateTimeOffset.Now.AddHours(-1),
    filter: "_PID=1234");
```

### Values with Spaces

Enclose values containing spaces in quotes:

```csharp
var logs = await journalService.GetLogs(
    since: DateTimeOffset.Now.AddHours(-1),
    filter: "MESSAGE=\"Connection refused\"");
```

---

## Combining Filters

### AND Logic (Comma Separator)

Use `,` to combine filters with AND logic. All conditions must match:

```csharp
// Priority 3 (error) AND from nginx
var logs = await journalService.GetLogs(
    since: DateTimeOffset.Now.AddHours(-1),
    filter: "PRIORITY=3,SYSLOG_IDENTIFIER=nginx");

// Multiple AND conditions
var logs = await journalService.GetLogs(
    since: DateTimeOffset.Now.AddHours(-1),
    filter: "PRIORITY=3,_UID=1000,_HOSTNAME=server1");
```

### OR Logic (Plus Separator)

Use `+` to combine filters with OR logic (disjunction). At least one condition must match:

```csharp
// Errors OR warnings
var logs = await journalService.GetLogs(
    since: DateTimeOffset.Now.AddHours(-1),
    filter: "PRIORITY=3+PRIORITY=4");

// From nginx OR apache
var logs = await journalService.GetLogs(
    since: DateTimeOffset.Now.AddHours(-1),
    filter: "SYSLOG_IDENTIFIER=nginx+SYSLOG_IDENTIFIER=apache");
```

### Combining AND and OR

You can combine both operators. AND (`,`) and OR (`+`) are evaluated based on the sequence:

```csharp
// (Priority 3 OR Priority 4) AND from specific host
// Note: The logic follows the pattern where + creates disjunctions
var logs = await journalService.GetLogs(
    since: DateTimeOffset.Now.AddHours(-1),
    filter: "PRIORITY=3+PRIORITY=4,_HOSTNAME=server1");
```

---

## Common Filter Patterns

### Filter by Log Level (Priority)

Priority levels follow syslog conventions:

| Priority | Level   | Description                      |
| -------- | ------- | -------------------------------- |
| 0        | EMERG   | System is unusable               |
| 1        | ALERT   | Action must be taken immediately |
| 2        | CRIT    | Critical conditions              |
| 3        | ERR     | Error conditions                 |
| 4        | WARNING | Warning conditions               |
| 5        | NOTICE  | Normal but significant           |
| 6        | INFO    | Informational messages           |
| 7        | DEBUG   | Debug-level messages             |

```csharp
// Errors only
filter: "PRIORITY=3"

// Errors and critical
filter: "PRIORITY=2+PRIORITY=3"

// Everything except debug
filter: "PRIORITY=0+PRIORITY=1+PRIORITY=2+PRIORITY=3+PRIORITY=4+PRIORITY=5+PRIORITY=6"

// All severe messages (EMERG through WARNING)
filter: "PRIORITY=0+PRIORITY=1+PRIORITY=2+PRIORITY=3+PRIORITY=4"
```

### Filter by Application

```csharp
// Single application
filter: "SYSLOG_IDENTIFIER=myapp"

// Multiple applications
filter: "SYSLOG_IDENTIFIER=nginx+SYSLOG_IDENTIFIER=php-fpm"

// By executable path
filter: "_EXE=/usr/bin/nginx"
```

### Filter by User/Process

```csharp
// By user ID
filter: "_UID=1000"

// By process ID
filter: "_PID=12345"

// By group ID
filter: "_GID=1000"

// Combination: specific user running specific app
filter: "_UID=1000,SYSLOG_IDENTIFIER=myapp"
```

### Filter by Machine/Host

```csharp
// By hostname
filter: "_HOSTNAME=webserver01"

// By machine ID
filter: "_MACHINE_ID=abc123..."
```

### Filter by Systemd Unit

```csharp
// By systemd unit
filter: "_SYSTEMD_UNIT=nginx.service"

// By user unit
filter: "_SYSTEMD_USER_UNIT=myapp.service"

// By slice
filter: "_SYSTEMD_SLICE=user-1000.slice"
```

---

## Available Journal Fields

### Standard Fields

| Field               | Description                              |
| ------------------- | ---------------------------------------- |
| `MESSAGE`           | The human-readable log message           |
| `MESSAGE_ID`        | 128-bit message identifier               |
| `PRIORITY`          | Syslog priority (0-7)                    |
| `CODE_FILE`         | Source code file                         |
| `CODE_LINE`         | Source code line number                  |
| `CODE_FUNC`         | Source code function                     |
| `ERRNO`             | Low-level Unix error number              |
| `SYSLOG_FACILITY`   | Syslog facility                          |
| `SYSLOG_IDENTIFIER` | Syslog identifier (usually program name) |
| `SYSLOG_PID`        | Syslog PID                               |

### Trusted Fields (Added by Journal)

These fields are prefixed with `_` and set by the journal daemon, not the client:

| Field                        | Description            |
| ---------------------------- | ---------------------- |
| `_PID`                       | Process ID             |
| `_UID`                       | User ID                |
| `_GID`                       | Group ID               |
| `_COMM`                      | Command name           |
| `_EXE`                       | Executable path        |
| `_CMDLINE`                   | Command line           |
| `_CAP_EFFECTIVE`             | Effective capabilities |
| `_AUDIT_SESSION`             | Audit session ID       |
| `_AUDIT_LOGINUID`            | Audit login UID        |
| `_SYSTEMD_CGROUP`            | Systemd cgroup path    |
| `_SYSTEMD_SLICE`             | Systemd slice          |
| `_SYSTEMD_UNIT`              | Systemd unit           |
| `_SYSTEMD_USER_UNIT`         | Systemd user unit      |
| `_SYSTEMD_SESSION`           | Systemd session        |
| `_SYSTEMD_OWNER_UID`         | Systemd owner UID      |
| `_SELINUX_CONTEXT`           | SELinux context        |
| `_SOURCE_REALTIME_TIMESTAMP` | Source timestamp       |
| `_BOOT_ID`                   | Boot ID                |
| `_MACHINE_ID`                | Machine ID             |
| `_HOSTNAME`                  | Hostname               |
| `_TRANSPORT`                 | Transport method       |

### Time Fields

| Field                 | Description                        |
| --------------------- | ---------------------------------- |
| `REALTIME_TIMESTAMP`  | Realtime timestamp in microseconds |
| `MONOTONIC_TIMESTAMP` | Monotonic timestamp                |
| `__CURSOR`            | Unique cursor for the entry        |

---

## Filter Examples

### Web Server Logs

```csharp
// All nginx errors
var logs = await journalService.GetLogs(
    since: DateTimeOffset.Now.AddHours(-24),
    filter: "SYSLOG_IDENTIFIER=nginx,PRIORITY=3");

// Web stack (nginx + php-fpm) errors and warnings
var logs = await journalService.GetLogs(
    since: DateTimeOffset.Now.AddHours(-1),
    filter: "SYSLOG_IDENTIFIER=nginx+SYSLOG_IDENTIFIER=php-fpm,PRIORITY=3+PRIORITY=4");
```

### Systemd Service Monitoring

```csharp
// All logs from a specific service
var logs = await journalService.GetLogs(
    since: DateTimeOffset.Now.AddHours(-1),
    filter: "_SYSTEMD_UNIT=myapp.service");

// Service errors only
var logs = await journalService.GetLogs(
    since: DateTimeOffset.Now.AddHours(-1),
    filter: "_SYSTEMD_UNIT=myapp.service,PRIORITY=3");

// Multiple services
var logs = await journalService.GetLogs(
    since: DateTimeOffset.Now.AddHours(-1),
    filter: "_SYSTEMD_UNIT=nginx.service+_SYSTEMD_UNIT=postgresql.service");
```

### Security Auditing

```csharp
// All root (UID 0) activity
var logs = await journalService.GetLogs(
    since: DateTimeOffset.Now.AddDays(-7),
    filter: "_UID=0");

// SSH daemon logs
var logs = await journalService.GetLogs(
    since: DateTimeOffset.Now.AddHours(-24),
    filter: "SYSLOG_IDENTIFIER=sshd");

// Authentication failures (example with custom field)
var logs = await journalService.GetLogs(
    since: DateTimeOffset.Now.AddHours(-1),
    filter: "_SYSTEMD_UNIT=systemd-logind.service,PRIORITY=4");
```

### Application Debugging

```csharp
// Debug logs from your application
var logs = await journalService.GetLogs(
    since: DateTimeOffset.Now.AddMinutes(-30),
    filter: "SYSLOG_IDENTIFIER=myapp,PRIORITY=7");

// All logs from current boot
// (combine with boot ID from GetFieldValues)
var bootId = (await journalService.GetFieldValues("_BOOT_ID")).First();
var logs = await journalService.GetLogs(
    since: DateTimeOffset.MinValue.AddYears(2000),
    filter: $"_BOOT_ID={bootId}");
```

### Using Filters with Sessions

```csharp
// With follow session
using var follower = await journalService.CreateFollower(TimeSpan.FromSeconds(1));
follower.SetFilter("PRIORITY=3+PRIORITY=4"); // Errors and warnings only
follower.NewEntry += entry => Console.WriteLine(entry["MESSAGE"]);
follower.Start(10);

// With look-behind session
using var lookBehind = await journalService.CreateGeneric().ToLookBehind();
lookBehind.SetFilter("SYSLOG_IDENTIFIER=nginx,PRIORITY=3");
foreach (var entry in lookBehind.GetLogs(100))
{
    Console.WriteLine(entry["MESSAGE"]);
}
```

---

## Tips and Best Practices

1. **Use specific filters**: More specific filters are more efficient as they're applied at the journal level.

2. **Combine with time ranges**: Always use appropriate `since` times to avoid scanning the entire journal.

3. **Test filters incrementally**: Start with simple filters and add conditions gradually.

4. **Check available fields**: Use `GetFields()` to see what fields are available in your journal.

5. **Case sensitivity**: Remember that both field names and values are case-sensitive.

6. **Escape special characters**: If values contain special characters, ensure proper escaping or quoting.

---

## See Also

- [Getting Started](getting-started.md) - Installation and basic usage
- [Configuration](configuration.md) - All configuration options
- [Troubleshooting](troubleshooting.md) - Common issues and solutions
