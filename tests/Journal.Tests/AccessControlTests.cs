using FluentAssertions;
using ViciOne.Journal;

namespace Journal.Tests;

public class AccessControl_
{
    [Fact]
    public async Task Throws_if_limit_reached()
    {
        using AccessControl accessControl = new(2);
        var access1 = await accessControl.Begin(CancellationToken.None);
        var access2 = await accessControl.Begin(CancellationToken.None);

        var act = FluentActions.Invoking(() => accessControl.Begin(CancellationToken.None));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Access limit reached. Only 2 concurrent accesses are permitted.");
        access1.Dispose();
        access2.Dispose();
    }
}
