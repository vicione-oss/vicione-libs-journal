using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using ViciOne.Journal;

namespace Logging.Tests;

public class JournalEntryFactory_CreateFieldName
{
    [Theory]
    [InlineData("TEST_FIELD", "TEST_FIELD")]
    [InlineData("_T#E*ST_FIEL()D", "TEST_FIELD")]
    [InlineData("testValue", "TEST_VALUE")]
    public void Creates_correct_name(string value, string expected)
        => JournalEntryFactory.CreateFieldName(value).Should().Be(expected);
}

public class JournalEntryFactory_MapLogLevelToJournalPriority
{
    [Theory]
    [InlineData(LogLevel.Trace, "7")]
    [InlineData(LogLevel.Debug, "7")]
    [InlineData(LogLevel.Information, "6")]
    [InlineData(LogLevel.Warning, "4")]
    [InlineData(LogLevel.Error, "3")]
    [InlineData(LogLevel.Critical, "2")]
    public void Maps_log_level_correctly(LogLevel logLevel, string expected)
        => JournalEntryFactory.MapLogLevelToJournalPriority(logLevel).Should().Be(expected);
}

public class JournalEntryFactory_CreateStructuredData
{
    [Fact]
    public void Creates_basic_structured_data()
    {
        var fields = new Dictionary<string, object?>
            {
                { "TEST_FIELD", "testValue" },
                { "NUM", 42 }
            };

        var result = JournalEntryFactory.CreateStructuredData(fields);

        result.Should().StartWith("[journal@32473");
        result.Should().Contain("TEST_FIELD=\"testValue\"");
        result.Should().Contain("NUM=\"42\"");
        result.Should().EndWith("]");
    }

    [Fact]
    public void Excludes_standard_syslog_fields()
    {
        var fields = new Dictionary<string, object?>
            {
                { "MESSAGE", "test message" },
                { "PRIORITY", "6" },
                { "SYSLOG_STRUCTURED_DATA", "existing" },
                { "TEST_FIELD", "value" }
            };

        var result = JournalEntryFactory.CreateStructuredData(fields);

        result.Should().NotContain("MESSAGE=");
        result.Should().NotContain("PRIORITY=");
        result.Should().NotContain("SYSLOG_STRUCTURED_DATA=");
        result.Should().Contain("TEST_FIELD=\"value\"");
    }

    [Fact]
    public void Excludes_all_rfc5424_standard_header_fields()
    {
        var fields = new Dictionary<string, object?>
            {
                // RFC 5424 header fields
                { "MESSAGE", "test message" },
                { "PRIORITY", "6" },
                { "VERSION", "1" },
                { "TIMESTAMP", "2024-01-01T00:00:00Z" },
                { "HOSTNAME", "testhost" },
                { "SYSLOG_IDENTIFIER", "myapp" },
                { "SYSLOG_PID", "1234" },
                { "MESSAGE_ID", "MSG001" },
                { "SYSLOG_FACILITY", "16" },
                { "REALTIME_TIMESTAMP", "1234567890" },
                { "MONOTONIC_TIMESTAMP", "9876543210" },

                // Process fields
                { "PID", "1234" },
                { "_PID", "1234" },
                { "_UID", "1000" },
                { "_GID", "1000" },
                { "_COMM", "myapp" },
                { "_EXE", "/usr/bin/myapp" },
                { "_CMDLINE", "myapp --config test.conf" },

                // System fields
                { "_HOSTNAME", "testhost" },
                { "_MACHINE_ID", "abc123" },
                { "_BOOT_ID", "def456" },

                // Systemd fields
                { "_SYSTEMD_UNIT", "myapp.service" },
                { "_SYSTEMD_USER_UNIT", "myapp.service" },
                { "_SYSTEMD_SLICE", "system.slice" },
                { "_SYSTEMD_CGROUP", "/system.slice/myapp.service" },
                { "_TRANSPORT", "journal" },
                { "_SOURCE_REALTIME_TIMESTAMP", "1234567890" },

                // Custom application field (should be included)
                { "APP_CUSTOM_FIELD", "custom value" }
            };

        var result = JournalEntryFactory.CreateStructuredData(fields);

        // Verify all RFC 5424 standard fields are excluded
        result.Should().NotContain("MESSAGE=");
        result.Should().NotContain("PRIORITY=");
        result.Should().NotContain("VERSION=");
        result.Should().NotContain("TIMESTAMP=");
        result.Should().NotContain("HOSTNAME=");
        result.Should().NotContain("SYSLOG_IDENTIFIER=");
        result.Should().NotContain("SYSLOG_PID=");
        result.Should().NotContain("MESSAGE_ID=");
        result.Should().NotContain("SYSLOG_FACILITY=");
        result.Should().NotContain("REALTIME_TIMESTAMP=");
        result.Should().NotContain("MONOTONIC_TIMESTAMP=");
        result.Should().NotContain("PID=");
        result.Should().NotContain("_PID=");
        result.Should().NotContain("_UID=");
        result.Should().NotContain("_GID=");
        result.Should().NotContain("_COMM=");
        result.Should().NotContain("_EXE=");
        result.Should().NotContain("_CMDLINE=");
        result.Should().NotContain("_HOSTNAME=");
        result.Should().NotContain("_MACHINE_ID=");
        result.Should().NotContain("_BOOT_ID=");
        result.Should().NotContain("_SYSTEMD_UNIT=");
        result.Should().NotContain("_SYSTEMD_USER_UNIT=");
        result.Should().NotContain("_SYSTEMD_SLICE=");
        result.Should().NotContain("_SYSTEMD_CGROUP=");
        result.Should().NotContain("_TRANSPORT=");
        result.Should().NotContain("_SOURCE_REALTIME_TIMESTAMP=");

        // Verify custom field is included
        result.Should().Contain("APP_CUSTOM_FIELD=\"custom value\"");
    }

    [Fact]
    public void Escapes_special_characters()
    {
        var fields = new Dictionary<string, object?>
            {
                { "FIELD1", "value with \\backslash" },
                { "FIELD2", "value with \"quotes\"" },
                { "FIELD3", "value with ]bracket" }
            };

        var result = JournalEntryFactory.CreateStructuredData(fields);

        result.Should().Contain("FIELD1=\"value with \\\\backslash\"");
        result.Should().Contain("FIELD2=\"value with \\\"quotes\\\"\"");
        result.Should().Contain("FIELD3=\"value with \\]bracket\"");
    }

    [Fact]
    public void Returns_empty_string_for_empty_fields()
    {
        var fields = new Dictionary<string, object?>();

        var result = JournalEntryFactory.CreateStructuredData(fields);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Returns_empty_string_when_only_excluded_fields()
    {
        var fields = new Dictionary<string, object?>
            {
                { "MESSAGE", "test" },
                { "PRIORITY", "6" }
            };

        var result = JournalEntryFactory.CreateStructuredData(fields);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Returns_empty_string_when_only_rfc5424_fields_present()
    {
        var fields = new Dictionary<string, object?>
            {
                { "MESSAGE", "test" },
                { "PRIORITY", "6" },
                { "HOSTNAME", "testhost" },
                { "_PID", "1234" },
                { "_UID", "1000" }
            };

        var result = JournalEntryFactory.CreateStructuredData(fields);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Handles_null_values()
    {
        var fields = new Dictionary<string, object?>
            {
                { "FIELD1", null },
                { "FIELD2", "value" }
            };

        var result = JournalEntryFactory.CreateStructuredData(fields);

        result.Should().Contain("FIELD1=\"\"");
        result.Should().Contain("FIELD2=\"value\"");
    }

    [Fact]
    public void Uses_custom_sd_id()
    {
        var fields = new Dictionary<string, object?>
            {
                { "TEST", "value" }
            };

        var result = JournalEntryFactory.CreateStructuredData(fields, "custom@12345");

        result.Should().StartWith("[custom@12345");
        result.Should().Contain("TEST=\"value\"");
    }

    [Fact]
    public void Handles_empty_string_values()
    {
        var fields = new Dictionary<string, object?>
            {
                { "EMPTY", "" },
                { "NORMAL", "value" }
            };

        var result = JournalEntryFactory.CreateStructuredData(fields);

        result.Should().Contain("EMPTY=\"\"");
        result.Should().Contain("NORMAL=\"value\"");
    }

    [Fact]
    public void Handles_complex_unicode_values()
    {
        var fields = new Dictionary<string, object?>
            {
                { "UNICODE", "Hello 世界 🌍" }
            };

        var result = JournalEntryFactory.CreateStructuredData(fields);

        result.Should().Contain("UNICODE=\"Hello 世界 🌍\"");
    }
}

public class JournalLogger_CreateLogEntry
{
    [Fact]
    public void Adds_structured_data_field_to_log_entry()
    {
        var keyValueList = new List<KeyValuePair<string, object?>>
            {
                new("testField", "testValue"),
                new("numField", 123)
            };

        var result = JournalEntryFactory.CreateLogEntry(keyValueList, "Test message", LogLevel.Information, false, true);

        result.Should().ContainKey("SYSLOG_STRUCTURED_DATA");
        result["SYSLOG_STRUCTURED_DATA"].Should().NotBeNull();
        result["SYSLOG_STRUCTURED_DATA"]?.ToString().Should().StartWith("[journal@32473");
    }

    [Fact]
    public void Structured_data_contains_all_fields()
    {
        var keyValueList = new List<KeyValuePair<string, object?>>
            {
                new("testField", "testValue"),
                new("numField", 123)
            };

        var result = JournalEntryFactory.CreateLogEntry(keyValueList, "Test message", LogLevel.Information, false, true);

        var structuredData = result["SYSLOG_STRUCTURED_DATA"]?.ToString();
        // Field names are transformed: testField -> TEST_FIELD, numField -> NUM_FIELD
        structuredData.Should().Contain("TEST_FIELD=\"testValue\"");
        structuredData.Should().Contain("NUM_FIELD=\"123\"");
    }

    [Fact]
    public void Skips_structured_data_field_when_no_custom_fields()
    {
        var keyValueList = new List<KeyValuePair<string, object?>>();

        var result = JournalEntryFactory.CreateLogEntry(keyValueList, "Test message", LogLevel.Information, false, true);

        // SYSLOG_STRUCTURED_DATA field should not be present when there are no custom fields
        result.Should().NotContainKey("SYSLOG_STRUCTURED_DATA");
    }

    [Fact]
    public void Custom_fields_do_not_overwrite_message_and_priority()
    {
        var keyValueList = new List<KeyValuePair<string, object?>>
            {
                new("Message", "raw value"),
                new("Priority", "high")
            };

        var result = JournalEntryFactory.CreateLogEntry(keyValueList, "rendered message", LogLevel.Error, true, false);

        result["MESSAGE"].Should().Be("rendered message");
        result["PRIORITY"].Should().Be("3");
        result["PROP_MESSAGE"].Should().Be("raw value");
        result["PROP_PRIORITY"].Should().Be("high");
    }

    [Fact]
    public void Custom_fields_do_not_overwrite_structured_data()
    {
        var keyValueList = new List<KeyValuePair<string, object?>>
            {
                new("testField", "testValue"),
                new("SyslogStructuredData", "raw value")
            };

        var result = JournalEntryFactory.CreateLogEntry(keyValueList, "Test message", LogLevel.Information, true, true);

        result["SYSLOG_STRUCTURED_DATA"].Should().Be("[journal@32473 TEST_FIELD=\"testValue\"]");
        result["PROP_SYSLOG_STRUCTURED_DATA"].Should().Be("raw value");
    }

    [Theory]
    [InlineData("SyslogIdentifier", "SYSLOG_IDENTIFIER")]
    [InlineData("SyslogFacility", "SYSLOG_FACILITY")]
    [InlineData("SyslogPid", "SYSLOG_PID")]
    [InlineData("MessageId", "MESSAGE_ID")]
    [InlineData("CodeFile", "CODE_FILE")]
    [InlineData("Errno", "ERRNO")]
    [InlineData("Unit", "UNIT")]
    public void Custom_fields_do_not_set_reserved_journal_fields(string key, string fieldName)
    {
        var keyValueList = new List<KeyValuePair<string, object?>> { new(key, "raw value") };

        var result = JournalEntryFactory.CreateLogEntry(keyValueList, "Test message", LogLevel.Information, true, false);

        result.Should().NotContainKey(fieldName);
        result[$"PROP_{fieldName}"].Should().Be("raw value");
    }

    [Fact]
    public void Meta_data_sets_reserved_journal_fields()
    {
        var keyValueList = new List<KeyValuePair<string, object?>> { new("SyslogIdentifier", "raw value") };
        var metaData = new Dictionary<string, object?>
            {
                { "SYSLOG_IDENTIFIER", "MyApp" },
                { "SYSLOG_FACILITY", 16 },
                { "UNIT", "my.service" }
            };

        var result = JournalEntryFactory.CreateLogEntry(keyValueList, "Test message", LogLevel.Information, true, false,
            metaData: metaData);

        result["SYSLOG_IDENTIFIER"].Should().Be("MyApp");
        result["SYSLOG_FACILITY"].Should().Be(16);
        result["UNIT"].Should().Be("my.service");
        result["PROP_SYSLOG_IDENTIFIER"].Should().Be("raw value");
    }

    [Fact]
    public void Writes_other_custom_fields_unchanged()
    {
        var keyValueList = new List<KeyValuePair<string, object?>>
            {
                new("SourceContext", "MyEngine"),
                new("EventId", 42)
            };

        var result = JournalEntryFactory.CreateLogEntry(keyValueList, "Test message", LogLevel.Information, true, false);

        result["SOURCE_CONTEXT"].Should().Be("MyEngine");
        result["EVENT_ID"].Should().Be(42);
    }
}
