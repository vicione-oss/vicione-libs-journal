# Changelog

## 1.1.0 - unreleased

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
