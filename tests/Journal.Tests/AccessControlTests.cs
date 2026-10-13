using AwesomeAssertions;
using ViciOne.Journal;

namespace Journal.Tests;

public class AccessControl_Acquire
{
    [Fact]
    public void Throws_a_busy_exception_when_all_slots_are_taken()
    {
        using AccessControl accessControl = new(2, "busy");
        using var slot1 = accessControl.Acquire();
        using var slot2 = accessControl.Acquire();

        var act = accessControl.Acquire;

        act.Should().Throw<JournalBusyException>().WithMessage("busy");
    }

    [Fact]
    public void Releases_a_slot_once_however_often_it_is_disposed()
    {
        using AccessControl accessControl = new(2, "busy");
        using var held = accessControl.Acquire();
        var released = accessControl.Acquire();

        released.Dispose();
        released.Dispose();

        using var slot = accessControl.Acquire();
        var act = accessControl.Acquire;
        act.Should().Throw<JournalBusyException>();
    }
}
