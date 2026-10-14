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

    [Fact]
    public void Surfaces_event_id_with_name()
    {
        IReadOnlyDictionary<string, object?> result = new Dictionary<string, object?>();
        var logger = new JournalLogger("", "Test", null, entry => result = entry, new Dictionary<string, object?>(), true, true);

        logger.LogWarning(new EventId(1001, "RequestFailed"), "Request {RequestId} failed", 1001);

        result.Should().ContainKey("EVENT_ID").WhoseValue.Should().Be("1001:RequestFailed");
    }

    [Fact]
    public void Surfaces_event_id_when_custom_key_values_are_disabled_and_it_is_an_aggregation_field()
    {
        IReadOnlyDictionary<string, object?> result = new Dictionary<string, object?>();
        var logger = new JournalLogger("", "Test", null, entry => result = entry, new Dictionary<string, object?>(),
            false, false, new HashSet<string> { "CATEGORY", "EVENT_ID" });

        logger.LogWarning(new EventId(1001, "RequestFailed"), "Request {RequestId} failed", 1001);

        result.Should().ContainKey("EVENT_ID").WhoseValue.Should().Be("1001:RequestFailed");
        result.Should().NotContainKey("REQUESTID");
    }

    [Fact]
    public void Does_not_surface_event_id_when_custom_key_values_disabled_and_spam_guard_off()
    {
        // Default configuration (WriteCustomKeyValues=false, spam guard disabled) must not
        // gain a new EVENT_ID field on upgrade - see regression covered by review. CATEGORY is
        // pre-existing and unaffected (see Category_behavior_is_unchanged_* below).
        IReadOnlyDictionary<string, object?> result = new Dictionary<string, object?>();
        var logger = new JournalLogger("", "Test", null, entry => result = entry, new Dictionary<string, object?>(), false, false);

        logger.LogWarning(new EventId(1001, "RequestFailed"), "Request {RequestId} failed", 1001);

        result.Should().NotContainKey("EVENT_ID");
    }

    [Fact]
    public void Does_not_leak_event_id_into_structured_data_when_custom_key_values_disabled_and_spam_guard_off()
    {
        // The default (WriteCustomKeyValues=false, WriteStructuredData=true, spam guard off)
        // must not leak EVENT_ID into SYSLOG_STRUCTURED_DATA either - a top-level gate alone
        // isn't enough, because CreateLogEntry builds structured data from every field it's
        // handed, independent of WriteCustomKeyValues.
        IReadOnlyDictionary<string, object?> result = new Dictionary<string, object?>();
        var logger = new JournalLogger("", "Test", null, entry => result = entry, new Dictionary<string, object?>(), false, true);

        logger.LogWarning(new EventId(1001, "RequestFailed"), "Request {RequestId} failed", 1001);

        result.Should().NotContainKey("EVENT_ID");
        result["SYSLOG_STRUCTURED_DATA"]?.ToString().Should().NotContain("EVENT_ID");
    }

    [Fact]
    public void Does_not_surface_event_id_when_only_category_is_an_aggregation_field()
    {
        // A field must only be forced when it's actually part of the resolved AggregationFields
        // - being merely "spam guard enabled" is not enough.
        IReadOnlyDictionary<string, object?> result = new Dictionary<string, object?>();
        var logger = new JournalLogger("", "Test", null, entry => result = entry, new Dictionary<string, object?>(),
            false, false, new HashSet<string> { "CATEGORY" });

        logger.LogWarning(new EventId(1001, "RequestFailed"), "Request {RequestId} failed", 1001);

        result.Should().NotContainKey("EVENT_ID");
    }

    [Fact]
    public void Category_behavior_is_unchanged_regardless_of_custom_key_values_or_spam_guard()
    {
        // CATEGORY pre-dates the EventId/MessageTemplate spam-guard work and must keep its
        // exact pre-existing behavior: always in SYSLOG_STRUCTURED_DATA when WriteStructuredData
        // is true, regardless of WriteCustomKeyValues or spam guard configuration.
        IReadOnlyDictionary<string, object?> result = new Dictionary<string, object?>();
        var logger = new JournalLogger("", "Test", null, entry => result = entry, new Dictionary<string, object?>(),
            false, true, new HashSet<string> { "CATEGORY" });

        logger.LogInformation("Hello");

        result.Should().NotContainKey("CATEGORY");
        result["SYSLOG_STRUCTURED_DATA"]?.ToString().Should().Contain("CATEGORY=\"Test\"");
    }

    [Fact]
    public void Does_not_let_template_property_impersonate_reserved_event_id_field()
    {
        IReadOnlyDictionary<string, object?> result = new Dictionary<string, object?>();
        var logger = new JournalLogger("", "Test", null, entry => result = entry, new Dictionary<string, object?>(), true, true);

        // No real EventId struct is supplied, but the message template has a placeholder
        // literally named "EventId" (a common ILogger correlation-id pattern), which
        // CreateFieldName would otherwise normalize to the reserved "EVENT_ID" key.
        logger.LogInformation("Business event {EventId} occurred", "BUSINESS-42");

        result.Should().NotContainKey("EVENT_ID");
    }

    [Fact]
    public void Surfaces_numeric_event_id_without_name()
    {
        IReadOnlyDictionary<string, object?> result = new Dictionary<string, object?>();
        var logger = new JournalLogger("", "Test", null, entry => result = entry, new Dictionary<string, object?>(), true, true);

        logger.LogWarning(new EventId(1001), "Request {RequestId} failed", 1001);

        result.Should().ContainKey("EVENT_ID").WhoseValue.Should().Be(1001);
    }

    [Fact]
    public void Omits_event_id_when_default()
    {
        IReadOnlyDictionary<string, object?> result = new Dictionary<string, object?>();
        var logger = new JournalLogger("", "Test", null, entry => result = entry, new Dictionary<string, object?>(), true, true);

        logger.LogWarning("Request {RequestId} failed", 1001);

        result.Should().NotContainKey("EVENT_ID");
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
