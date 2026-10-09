using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ViciOne.Journal;

namespace Journal.Tests;

[Trait("Category", "Integration")]
public class JournalOptions_Directory
{
    [Fact]
    public async Task Reads_the_journal_files_of_the_directory_Async()
    {
        var machineId = (await File.ReadAllTextAsync("/etc/machine-id", TestContext.Current.CancellationToken)).Trim();
        using var service = new JournalService(NullLoggerFactory.Instance, Options.Create(new JournalOptions { Directory = $"/var/log/journal/{machineId}" }));

        var page = await service.ReadPage(new(new JournalFieldSet("MESSAGE")) { Count = 20 }, TestContext.Current.CancellationToken);

        page.Records.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Reads_nothing_from_a_directory_without_journal_files_Async()
    {
        var directory = Directory.CreateTempSubdirectory();
        using var service = new JournalService(NullLoggerFactory.Instance, Options.Create(new JournalOptions { Directory = directory.FullName }));

        var page = await service.ReadPage(new(new JournalFieldSet("MESSAGE")) { Count = 20 }, TestContext.Current.CancellationToken);

        page.Records.Should().BeEmpty();
        directory.Delete();
    }
}
