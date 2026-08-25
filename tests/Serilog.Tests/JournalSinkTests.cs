using System.Globalization;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Parsing;
using Serilog.Sinks.Journal;
using ViciOne.Journal;

namespace Serilog.Tests;

public class JournalSink_
{
    [Fact]
    public void Can_create_instace()
    {
        if (OperatingSystem.IsLinux())
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Journal(CultureInfo.InvariantCulture)
                .CreateLogger();

            Log.CloseAndFlush();
        }
    }

    [Fact]
    public void Can_create_instace_with_meta_data()
    {
        if (OperatingSystem.IsLinux())
        {
            Log.Logger = new LoggerConfiguration()
            .WriteTo.Journal(CultureInfo.InvariantCulture, metaDataFields: new Dictionary<string, object?>()
            {
                { "TEST_ID", 1},
            })
            .CreateLogger();

            Log.CloseAndFlush();
        }
    }

    [Fact]
    public void Meta_data_takes_precedence_over_log_event_properties()
    {
        var logEvent = new LogEvent(DateTimeOffset.Now, LogEventLevel.Information, null,
            new MessageTemplateParser().Parse("{TestId} is a test"), [new LogEventProperty("TestId", new ScalarValue(2))]);
        var metaData = new Dictionary<string, object?>()
        {
            { "TEST_ID", 1 },
        };

        var result = JournalSink.CreateEntry(logEvent, new MessageTemplateTextFormatter("{Message}", CultureInfo.InvariantCulture),
            metaData, true, true);

        result["MESSAGE"].Should().Be("2 is a test");
        result["TEST_ID"].Should().Be(1);
        result["SYSLOG_STRUCTURED_DATA"].Should().Be("[journal@32473 TEST_ID=\"1\" MESSAGE_TEMPLATE=\"{TestId} is a test\"]");
    }

    [Fact]
    public void Message_template_field_cannot_be_overridden_by_log_property()
    {
        var messageTemplate = new MessageTemplateParser().Parse("Request {MESSAGE_TEMPLATE} failed");
        var logEvent = new LogEvent(DateTimeOffset.Now, LogEventLevel.Warning, null, messageTemplate,
        [
            new LogEventProperty("MESSAGE_TEMPLATE", new ScalarValue(1001)),
        ]);

        var fields = JournalSink.GetFieldsFromLogEvent(logEvent).ToList();

        fields.Should().ContainSingle(field => field.Key == "MESSAGE_TEMPLATE")
            .Which.Value.Should().Be("Request {MESSAGE_TEMPLATE} failed");
    }

    [Fact]
    public void Message_template_field_cannot_be_impersonated_by_differently_cased_log_property()
    {
        var messageTemplate = new MessageTemplateParser().Parse("Request {MessageTemplate} failed");
        var logEvent = new LogEvent(DateTimeOffset.Now, LogEventLevel.Warning, null, messageTemplate,
        [
            new LogEventProperty("MessageTemplate", new ScalarValue(1001)),
        ]);

        var fields = JournalSink.GetFieldsFromLogEvent(logEvent).ToList();

        fields.Should().ContainSingle(field => field.Key == "MessageTemplate" || field.Key == "MESSAGE_TEMPLATE")
            .Which.Value.Should().Be("Request {MessageTemplate} failed");
    }

    [Fact]
    public void Excludes_message_template_from_fields_when_not_requested()
    {
        var messageTemplate = new MessageTemplateParser().Parse("Request {RequestId} failed");
        var logEvent = new LogEvent(DateTimeOffset.Now, LogEventLevel.Warning, null, messageTemplate,
        [
            new LogEventProperty("RequestId", new ScalarValue(1001)),
        ]);

        var fields = JournalSink.GetFieldsFromLogEvent(logEvent, includeMessageTemplate: false).ToList();

        fields.Should().NotContain(field => field.Key == "MESSAGE_TEMPLATE");
    }

    [Fact]
    public void Does_not_leak_message_template_into_structured_data_when_custom_key_values_disabled()
    {
        // Mirrors what Emit() does: the default (writeCustomKeyValues=false,
        // writeStructuredData=true, spam guard off) must not leak MESSAGE_TEMPLATE into
        // SYSLOG_STRUCTURED_DATA. A top-level gate alone isn't enough, because CreateLogEntry
        // builds structured data from every field it's handed, independent of
        // writeCustomKeyValues.
        var messageTemplate = new MessageTemplateParser().Parse("Request {RequestId} failed");
        var logEvent = new LogEvent(DateTimeOffset.Now, LogEventLevel.Warning, null, messageTemplate,
        [
            new LogEventProperty("RequestId", new ScalarValue(1001)),
        ]);

        var fields = JournalSink.GetFieldsFromLogEvent(logEvent, includeMessageTemplate: false);
        var entry = JournalEntryFactory.CreateLogEntry(fields, "Request 1001 failed", LogLevel.Warning,
            writeCustomKeyValues: false, writeStructuredData: true);

        entry.Should().NotContainKey("MESSAGE_TEMPLATE");
        entry["SYSLOG_STRUCTURED_DATA"]?.ToString().Should().NotContain("MESSAGE_TEMPLATE");
    }

#pragma warning disable xUnit1004
    [Fact(Skip = "Creates real journal entries")]
#pragma warning restore xUnit1004
    public void Can_log_value()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Journal(CultureInfo.InvariantCulture)
            .CreateLogger();

        Log.Information("{Test_Value} is a test", "this");
    }

#pragma warning disable xUnit1004
    [Fact(Skip = "Creates real journal entries")]
#pragma warning restore xUnit1004
    public void Can_log_value_with_meta_data()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Journal(CultureInfo.InvariantCulture, metaDataFields: new Dictionary<string, object?>()
            {
                { "TEST_ID", 1},
            },
            outputTemplate: $$"""{{"engine.Name"}} ({{"engine.Id"}}): [{SourceContext}] {Message:lj}{NewLine}{Exception}""")
            .CreateLogger()
            .ForContext<JournalSink_>();

        Log.Information("{Test_Value} is a test", "this");
    }
}
