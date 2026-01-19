# Troubleshooting Guide

This guide helps you diagnose and resolve common issues when using ViciOne.Journal.

## Table of Contents

- [Platform Requirements](#platform-requirements)
- [Common Errors](#common-errors)
- [Permission Issues](#permission-issues)
- [Journal Opening Modes](#journal-opening-modes)
- [Performance Issues](#performance-issues)
- [Debugging Tips](#debugging-tips)

---

## Platform Requirements

### Supported Platforms

| Platform            | Supported | Notes                        |
| ------------------- | --------- | ---------------------------- |
| Linux (systemd)     | ✅ Yes     | Primary target platform      |
| Linux (non-systemd) | ❌ No      | Requires systemd             |
| Windows             | ❌ No      | No systemd support           |
| macOS               | ❌ No      | No systemd support           |
| Docker (Linux)      | ✅ Yes     | Requires systemd access      |
| WSL2                | ⚠️ Limited | Depends on WSL configuration |

### Prerequisites Checklist

```bash
# 1. Verify systemd is running
systemctl --version
# Expected: systemd 245 or later

# 2. Check libsystemd is available
ldconfig -p | grep libsystemd
# Expected: libsystemd.so.0 -> /lib/x86_64-linux-gnu/libsystemd.so.0.x.x

# 3. Verify journal service is active
systemctl status systemd-journald
# Expected: Active: active (running)

# 4. Check journal files exist
ls -la /var/log/journal/ 2>/dev/null || ls -la /run/log/journal/
# Expected: Directory listing with journal files

# 5. Verify .NET version
dotnet --version
# Expected: 8.0.0 or later
```

---

## Common Errors

### Error: DllNotFoundException

**Symptom:**

```
System.DllNotFoundException: Unable to load shared library 'libsystemd.so.0'
```

**Causes:**

1. Running on a non-Linux platform
2. libsystemd is not installed
3. Library path is not configured

**Solutions:**

```bash
# Install libsystemd (Debian/Ubuntu)
sudo apt-get install libsystemd0 libsystemd-dev

# Install libsystemd (RHEL/CentOS/Fedora)
sudo dnf install systemd-libs systemd-devel

# Verify installation
ldconfig -p | grep libsystemd

# If library exists but not found, add to LD_LIBRARY_PATH
export LD_LIBRARY_PATH=/usr/lib/x86_64-linux-gnu:$LD_LIBRARY_PATH
```

---

### Error: sd_journal_open failed

**Symptom:**

```
ViciOne.Journal.JournalException: sd_journal_open failed with error -13
```

**Error Codes:**

| Code         | Meaning                 | Solution                          |
| ------------ | ----------------------- | --------------------------------- |
| -1 (EPERM)   | Operation not permitted | Check permissions                 |
| -2 (ENOENT)  | No journal files        | Verify journald is running        |
| -13 (EACCES) | Permission denied       | Add user to systemd-journal group |
| -22 (EINVAL) | Invalid argument        | Check JournalOpeningMode flags    |

**Solutions:**

```bash
# Add user to systemd-journal group
sudo usermod -a -G systemd-journal $USER

# Verify group membership (logout/login required)
groups

# Check journal file permissions
ls -la /var/log/journal/*/

# For containerized apps, mount journal directory
docker run -v /var/log/journal:/var/log/journal:ro myapp
```

---

## Journal Opening Modes

### Mode Selection Guide

| Mode          | Use Case                                    |
| ------------- | ------------------------------------------- |
| `None`        | Access all available journals               |
| `LocalOnly`   | Exclude remote/network journals (default)   |
| `RuntimeOnly` | Only volatile (in-memory) journals          |
| `System`      | Only system journal (exclude user journals) |
| `CurrentUser` | Only current user's journal                 |

### Common Configurations

```csharp
// Default: Local system and user journals
options.OpeningMode = JournalOpeningMode.LocalOnly;

// System services only
options.OpeningMode = JournalOpeningMode.LocalOnly | JournalOpeningMode.System;

// User application logs
options.OpeningMode = JournalOpeningMode.CurrentUser;

// Volatile logs (for debugging)
options.OpeningMode = JournalOpeningMode.RuntimeOnly;
```

### Troubleshooting Modes

```csharp
// If you're not seeing expected logs, try None first
options.OpeningMode = JournalOpeningMode.None;

// Then narrow down
if (/* logs visible */)
{
    // Try more restrictive modes
}
```

---

## Performance Issues

### High Memory Usage

**Cause:**
Loading too many log entries into memory.

**Solutions:**

1. **Stream processing:**

```csharp
// Instead of loading all at once
foreach (var entry in session.GetLogs(1000))
{
    ProcessEntry(entry);
    // Entry can be garbage collected after processing
}
```

1. **Reduce limit:**

```csharp
// Smaller batches
var logs = await journalService.GetLogs(
    since: DateTimeOffset.Now.AddHours(-1),
    limit: 50);  // Not 10000
```

---

## Debugging Tips

### Enable Internal Logging

```csharp
builder.Services.Configure<JournalOptions>(options =>
{
    options.EnableLogging = true;  // Enable diagnostic logs
});

// Also configure your logging to show Debug level
builder.Logging.SetMinimumLevel(LogLevel.Debug);
```

### Check Journal Directly

```bash
# View recent journal entries
journalctl -n 50

# Follow live (like tail -f)
journalctl -f

# Filter by your application
journalctl SYSLOG_IDENTIFIER=MyApp -n 20

# Show with all fields (JSON format)
journalctl -o json-pretty -n 5

# Check for your custom fields
journalctl -o verbose SYSLOG_IDENTIFIER=MyApp | head -100
```

### Test Filter Syntax

```bash
# Test filter in journalctl first
journalctl PRIORITY=3 SYSLOG_IDENTIFIER=nginx -n 10

# If that works, use in code:
filter: "PRIORITY=3,SYSLOG_IDENTIFIER=nginx"
```

---

## Getting Help

If you're still experiencing issues:

1. **Test with journalctl** to verify the journal works
2. **Review permissions** for the running user
3. **Create a minimal reproduction** case

---

## See Also

- [Getting Started](getting-started.md) - Installation and basic usage
- [Configuration](configuration.md) - All configuration options
- [Architecture](architecture.md) - Understanding the library design
