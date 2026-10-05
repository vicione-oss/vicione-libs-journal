using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ViciOne.Journal;

namespace Logging.Tests;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1848:LoggerMessage-Delegaten verwenden")]
public class JournalLogger_
{
    [Fact]
    public void Can_log_message_correctly()
    {
        IReadOnlyDictionary<string, object?> result = new Dictionary<string, object?>();
        var logger = new JournalLogger("pre ", "Test", null, entry => result = entry, new Dictionary<string, object?>(), true, true);

        logger.LogInformation("This is a {TEST_FIELD}", "test");

        result.Should().SatisfyRespectively(
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("MESSAGE", "pre This is a test")),
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("PRIORITY", "6")),
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("TEST_FIELD", "test")),
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("CATEGORY", "Test")),
            p =>
            {
                p.Key.Should().Be("SYSLOG_STRUCTURED_DATA");
                p.Value?.ToString().Should().Contain("TEST_FIELD=\"test\"").And.Contain("CATEGORY=\"Test\"");
            });
    }

    [Fact]
    public void Can_log_message_correctly_with_multiple_fields()
    {
        IReadOnlyDictionary<string, object?> result = new Dictionary<string, object?>();
        var logger = new JournalLogger("pre ", "Test", null, entry => result = entry, new Dictionary<string, object?>(), true, true);

        logger.LogInformation("This is a {TEST_FIELD}, {NUM}", "test", 6);

        result.Should().SatisfyRespectively(
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("MESSAGE", "pre This is a test, 6")),
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("PRIORITY", "6")),
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("TEST_FIELD", "test")),
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, int>("NUM", 6)),
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("CATEGORY", "Test")),
            p =>
            {
                p.Key.Should().Be("SYSLOG_STRUCTURED_DATA");
                p.Value?.ToString().Should().Contain("TEST_FIELD=\"test\"").And.Contain("NUM=\"6\"").And.Contain("CATEGORY=\"Test\"");
            });
    }

    [Fact]
    public void Can_log_message_correctly_with_mistakes()
    {
        IReadOnlyDictionary<string, object?> result = new Dictionary<string, object?>();
        var logger = new JournalLogger("pre ", "Test", null, entry => result = entry, new Dictionary<string, object?>(), true, true);

        logger.LogInformation("This is a {_TEST_FI#*ELD}", "test");

        result.Should().SatisfyRespectively(
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("MESSAGE", "pre This is a test")),
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("PRIORITY", "6")),
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("TEST_FIELD", "test")),
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("CATEGORY", "Test")),
            p => p.Key.Should().Be("SYSLOG_STRUCTURED_DATA"));
    }

    [Fact]
    public void Can_log_message_correctly_with_no_valid_characters()
    {
        IReadOnlyDictionary<string, object?> result = new Dictionary<string, object?>();
        var logger = new JournalLogger("pre ", "Test", null, entry => result = entry, new Dictionary<string, object?>(), true, true);

        logger.LogInformation("This is a {_***}", "test");

        result.Should().SatisfyRespectively(
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("MESSAGE", "pre This is a test")),
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("PRIORITY", "6")),
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("CATEGORY", "Test")),
            p =>
            {
                p.Key.Should().Be("SYSLOG_STRUCTURED_DATA");
                p.Value?.ToString().Should().Contain("CATEGORY=\"Test\"");
            });
    }

    [Fact]
    public void Ingnores_empty_prefix()
    {
        IReadOnlyDictionary<string, object?> result = new Dictionary<string, object?>();
        var logger = new JournalLogger("", "Test", null, entry => result = entry, new Dictionary<string, object?>(), true, true);

        logger.LogInformation("This is a {TEST_FIELD}", "test");

        result.Should().SatisfyRespectively(
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("MESSAGE", "This is a test")),
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("PRIORITY", "6")),
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("TEST_FIELD", "test")),
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("CATEGORY", "Test")),
            p => p.Key.Should().Be("SYSLOG_STRUCTURED_DATA"));
    }

    [Fact]
    public void Can_use_meta_data()
    {
        IReadOnlyDictionary<string, object?> result = new Dictionary<string, object?>();
        var logger = new JournalLogger("", "Test", null, entry => result = entry, new Dictionary<string, object?>()
        {
            { "BLOCK_ID", "1" }
        }, true, true);

        logger.LogInformation("This is a {TEST_FIELD}", "test");

        result.Should().SatisfyRespectively(
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("MESSAGE", "This is a test")),
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("PRIORITY", "6")),
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("TEST_FIELD", "test")),
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("BLOCK_ID", "1")),
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("CATEGORY", "Test")),
            p =>
            {
                p.Key.Should().Be("SYSLOG_STRUCTURED_DATA");
                p.Value?.ToString().Should().Contain("BLOCK_ID=\"1\"");
            });
    }

    [Fact]
    public void Can_use_reserved_field_names_in_meta_data()
    {
        IReadOnlyDictionary<string, object?> result = new Dictionary<string, object?>();
        var logger = new JournalLogger("", "Test", null, entry => result = entry, new Dictionary<string, object?>()
        {
            { "SYSLOG_IDENTIFIER", "MyApp" }
        }, true, true);

        logger.LogInformation("This is a {SyslogIdentifier}", "test");

        result.Should().SatisfyRespectively(
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("MESSAGE", "This is a test")),
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("PRIORITY", "6")),
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("PROP_SYSLOG_IDENTIFIER", "test")),
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("SYSLOG_IDENTIFIER", "MyApp")),
            p => p.Should().BeEquivalentTo(new KeyValuePair<string, string>("CATEGORY", "Test")),
            p =>
            {
                p.Key.Should().Be("SYSLOG_STRUCTURED_DATA");
                p.Value?.ToString().Should().NotContain("SYSLOG_IDENTIFIER");
            });
    }

#pragma warning disable xUnit1004
    [Fact(Skip = "Creates real journal entries")]
#pragma warning restore xUnit1004
    public void Can_write_to_journal_with_service()
    {
        if (OperatingSystem.IsLinux())
        {
            var services = new ServiceCollection();
            services.AddLogging(c => c.AddJournal(options =>
            {
                options.OpenMode = JournalOpeningMode.LocalOnly;
            }));

            var logger = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true }).GetRequiredService<ILogger<int>>();

            logger.LogInformation("This is a {_TeST_FI#*ELD}", "test");
        }
    }

#pragma warning disable xUnit1004
    [Fact(Skip = "Creates real journal entries")]
#pragma warning restore xUnit1004
    public void Can_write_to_journal_with_service_in_all_loglevels()
    {
        if (OperatingSystem.IsLinux())
        {
            var services = new ServiceCollection();
            services.AddLogging(c => c.AddJournal(options =>
            {
                options.OpenMode = JournalOpeningMode.LocalOnly;
            }));

            var logger = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true }).GetRequiredService<ILogger<int>>();

            logger.LogInformation("This is a {_TeST_FI#*ELD}", "test");
            logger.LogCritical("This is a {_TeST_FI#*ELD}", "test");
            logger.LogDebug("This is a {_TeST_FI#*ELD}", "test");
            logger.LogError("This is a {_TeST_FI#*ELD}", "test");
            logger.LogWarning("This is a {_TeST_FI#*ELD}", "test");
            logger.LogTrace("This is a {_TeST_FI#*ELD}", "test");
        }
    }

#pragma warning disable xUnit1004
    [Fact(Skip = "Creates real journal entries")]
#pragma warning restore xUnit1004
    public async Task Can_write_to_journal_with_service_on_two_threads()
    {
        if (OperatingSystem.IsLinux())
        {
            var services = new ServiceCollection();
            services.AddLogging(c => c.AddJournal(options =>
            {
                options.OpenMode = JournalOpeningMode.LocalOnly;
            }));

            var logger = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true }).GetRequiredService<ILogger<int>>();

            var task = new Task(() => logger.LogInformation("This is a {_TeST_FI#*ELD}", "test"));
            var task2 = new Task(() => logger.LogInformation("This is a {_TeST_FI#*ELD}", "test"));

            task.Start();
            task2.Start();

            await task.WaitAsync(CancellationToken.None);
            await task2.WaitAsync(CancellationToken.None);
        }
    }
}
