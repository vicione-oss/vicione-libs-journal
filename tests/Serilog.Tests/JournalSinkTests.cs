using System.Globalization;
using AwesomeAssertions;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Parsing;
using Serilog.Sinks.Journal;

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
        result["SYSLOG_STRUCTURED_DATA"].Should().Be("[journal@32473 TEST_ID=\"1\"]");
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
