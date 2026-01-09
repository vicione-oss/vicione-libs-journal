using System.Globalization;
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
