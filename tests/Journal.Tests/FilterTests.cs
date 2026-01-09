using NSubstitute;
using ViciOne.Journal;

namespace Journal.Tests;

public class Filter_
{
    [Fact]
    public void Applies_single_term()
    {
        var addMatch = Substitute.For<Action<string>>();
        var addDisjunction = Substitute.For<Action>();

        Filter.Apply("Field=value", addMatch, addDisjunction);

        addMatch.Received(1).Invoke("Field=value");
        addDisjunction.DidNotReceive().Invoke();
    }

    [Fact]
    public void Applies_multiple_terms()
    {
        var addMatch = Substitute.For<Action<string>>();
        var addDisjunction = Substitute.For<Action>();

        Filter.Apply("Field1=value,Field2=value", addMatch, addDisjunction);

        Received.InOrder(() =>
        {
            addMatch("Field1=value");
            addMatch("Field2=value");
        });
        addDisjunction.DidNotReceive().Invoke();
    }


    [Fact]
    public void Applies_multiple_terms_with_disjunction()
    {
        var addMatch = Substitute.For<Action<string>>();
        var addDisjunction = Substitute.For<Action>();

        Filter.Apply("Field1=value+Field2=value", addMatch, addDisjunction);

        Received.InOrder(() =>
        {
            addMatch("Field1=value");
            addDisjunction();
            addMatch("Field2=value");
        });
    }

    [Fact]
    public void Applies_multiple_terms_with_disjunction_and_without()
    {
        var addMatch = Substitute.For<Action<string>>();
        var addDisjunction = Substitute.For<Action>();

        Filter.Apply("Field1=value+Field2=value,Field3=value", addMatch, addDisjunction);

        Received.InOrder(() =>
        {
            addMatch("Field1=value");
            addDisjunction();
            addMatch("Field2=value");
            addMatch("Field3=value");
        });
    }
}
