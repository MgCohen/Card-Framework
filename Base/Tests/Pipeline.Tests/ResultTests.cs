using Pipeline.Domain.Shared;
using Pipeline.Handlers.Commands;

namespace Pipeline.Tests;

public sealed class ResultTests
{
    [Test]
    public void From_Rejected_NotAccepted()
    {
        Result result = Result.From(Rejected("no mana"));

        Assert.That(result.Accepted, Is.False);
        Assert.That(result.Reason, Is.EqualTo("no mana"));
    }

    [Test]
    public void From_Accepted_IsAccepted()
    {
        Result result = Result.From(Accepted(null));

        Assert.That(result.Accepted, Is.True);
        Assert.That(result.Reason, Is.Null);
    }

    [Test]
    public void FromT_Accepted_ReturnsValue()
    {
        Assert.That(Result.From<int>(Accepted(5)).Value, Is.EqualTo(5));
    }

    [Test]
    public void FromT_Rejected_ReturnsDefault()
    {
        Assert.That(Result.From<int>(Rejected("no mana")).Value, Is.EqualTo(0));
    }

    [Test]
    public void FromT_NullForAReferenceType_ReturnsNull()
    {
        Assert.That(Result.From<string>(Accepted(null)).Value, Is.Null);
    }

    [Test]
    public void FromT_NullForAValueType_Throws()
    {
        Assert.Throws<InvalidOperationException>(new Action(() => Result.From<int>(Accepted(null))));
    }

    [Test]
    public void FromT_WrongType_Throws()
    {
        Assert.Throws<InvalidOperationException>(new Action(() => Result.From<int>(Accepted("five"))));
    }

    [Test]
    public void From_Listed_Throws()
    {
        Assert.Throws<InvalidOperationException>(new Action(() => Result.From(Listed())));
    }

    [Test]
    public void From_Running_Throws()
    {
        Envelope e = Listed();
        e.Start();

        Assert.Throws<InvalidOperationException>(new Action(() => Result.From(e)));
    }

    [Test]
    public void From_Faulted_Throws()
    {
        Envelope e = Listed();
        e.Fault();

        Assert.Throws<InvalidOperationException>(new Action(() => Result.From<int>(e)));
    }

    private static Envelope Listed()
    {
        return new Envelope(new MessageId(1), new Ping(), null);
    }

    private static Envelope Rejected(string reason)
    {
        Envelope e = Listed();
        e.Reject(reason);
        return e;
    }

    private static Envelope Accepted(object? result)
    {
        Envelope e = Listed();
        e.Accept(result);
        return e;
    }
}
