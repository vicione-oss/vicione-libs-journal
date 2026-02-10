using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ViciOne.Journal;

namespace Logging.Tests;

public class JournalLoggerExtensions_
{
    [Fact]
    public void Can_create_logger()
    {
        if (OperatingSystem.IsLinux())
        {
            var services = new ServiceCollection();
            services.AddLogging(c => c.AddJournal(options =>
            {
                options.OpenMode = JournalOpeningMode.LocalOnly;
            }));

            var logger = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true }).GetRequiredService<ILogger<int>>();

            logger.Should().BeOfType<Logger<int>>();
        }
    }
}
