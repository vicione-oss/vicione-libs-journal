# Changelog

## Unreleased

### Added

- `IJournalGenericSession.SeekRealTime(DateTimeOffset)` seeks to an instant regardless of the device time zone
- `IJournalService.GetPriorityHistogram` counts entries per time bucket, priority and systemd unit in one pass, with uniform buckets or explicit bucket edges
- `JournalOptions.MaxSessions` and `JournalOptions.MaxConcurrentQueries` limit open sessions and running queries separately
- `IJournalGenericSession.GetRecords` reads only the fields of a `JournalFieldSet`, with the timestamp as `DateTimeOffset`, the cursor and the priority, into a `JournalRecord`

### Fixed

- With `writeCustomKeyValues` enabled, log properties no longer overwrite `MESSAGE`, `PRIORITY` or `SYSLOG_STRUCTURED_DATA`, and no longer set fields that journald interprets (such as `SYSLOG_IDENTIFIER`, `SYSLOG_FACILITY`, `MESSAGE_ID`, `CODE_FILE`, `ERRNO` or `UNIT`). These properties are now written with a `PROP_` prefix, for example `PROP_MESSAGE`. Configured metadata fields are still written as-is
- Fix filters ignoring a `+` that follows a term repeated from, or contained in, an earlier term
- Fix `GetLogs` and `GetPriorityCounts` reading their times as local clock time, which shifted the range whenever the offset differed from the device time zone
- Fix `GetLogs` returning ascending order for `DESC` or any other case than `desc`
- Fix filter values in double quotes never matching; the quotes are removed and no longer required for values with spaces
- Fix follow sessions missing entries from journal files created after they opened, such as after a rotation
- Fix `IJournalFollowSession.Start(logCount)` delivering one recent entry less than requested
- Fix `SetFilter` on generic and follow sessions combining the new filter with the previous one instead of replacing it
- Fix follow sessions raising `Error` when they are stopped or disposed while reading new entries
- Fix `GetPriorityCounts(start, end)` throwing `KeyNotFoundException` for any interval that contains entries
- Fix a session that failed to open keeping its slot
- Fix disposing a session twice releasing two slots
- Fix a caller waiting indefinitely instead of failing when it raced another caller for the last slot

### Changed

- Serilog sink: when a metadata field and a log event property map to the same field name, the metadata field now takes precedence, as it already did in `JournalLogger`
- Reverse `GetLogs` results for `desc` in linear instead of quadratic time
- Count `GetPriorityCounts` without allocating per entry
- `GetPriorityCounts` throws `OperationCanceledException` when cancelled and `ArgumentOutOfRangeException` for a `since` in the future, instead of `InvalidOperationException`
- Open sessions no longer use up the slots of one-shot queries such as `GetLogs`
- Throw `JournalBusyException`, an `InvalidOperationException`, when a limit is reached
- Run one-shot queries and session creation off the caller's thread
- Reuse up to `MaxConcurrentQueries` open journals for one-shot queries instead of opening one per call, which took about 30 ms on a desktop

## 1.2.0 - 2026-04-10

### Changed

- Log entries that are only duplicated once are now forwarded as-is instead of being replaced with a `"(repeated 1 times in ...)"` summary
- Spam summary message appendix now displays window durations in seconds for second-scale windows (sub-second windows remain in milliseconds)

### Fixed

- Fixed a memory leak where one-shot log entries (never duplicated) permanently occupied spam guard bucket slots, eventually causing all new entries to bypass aggregation

## 1.1.0 - 2026-02-25

### Added

- Add spam-guard support to Serilog sink

## 1.0.0 - 2026-02-17

### Added

- Support structured data
- Implement spam-guard
- Add an interface for journal service
- Add an interface for journal session

### Changed

- Update `.NET` to `10.0`
- Update `Microsoft.Extensions.Hosting.Abstractions` to `10.0.3`
- Update `Microsoft.Extensions.Logging.Abstractions` to `10.0.3`
- Update `Microsoft.Extensions.Logging.Configuration` to `10.0.3`
- Update `Serilog` to `4.3.1`

### Fixed

- Handle empty Entries
- Make JournalLogger entry creation thread safe
- Free cursor pointer of systemd journal after use
- Avoid using invalid field names
- Use UTF-8 everywhere instead of ANSI
- Avoid GC collection of intermediate objects when writing log entries

## 0.7.0 - 2025-07-01

### Added

- Support for Serilog

## 0.6.0 - 2025-06-13

### Added

- Add message prefix option to JournalLogger
- Add meta data option to JournalLogger
- Support journal opening mode 0 (default)
- Support configuration of journal opening mode in sessions

### Removed

- **Breaking:** Remove unused journal opening modes

### Fixed

- Remove misspelling in debug output

## 0.5.0 - 2025-05-26

### Added

- Provide option to enable or disable logging

## 0.4.0 - 2025-05-19

### Added

- Implement logger that writes in the journal
- Provide generic journal session

### Changed

- Use generic session for look behind
- Downgrade some journal log messages to debug level

### Removed

- **Breaking:** Remove direct function to create look behind session at journal service

## 0.3.0 - 2025-03-28

### Added

- Implement follow session

## 0.2.0 - 2025-03-18

### Changed

- Remove unnecessary checks in AddMatch

## 0.1.0 - 2025-03-11

- Initial Release
