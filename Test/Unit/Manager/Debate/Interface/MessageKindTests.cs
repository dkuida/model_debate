using ModelDebate.Manager.Debate.Interface;
using NUnit.Framework;

namespace Test.Unit.Manager.Debate;

[TestFixture]
public class MessageKindTests
{
    [Test]
    public void MessageKind_HasSeedValue()
    {
        MessageKind kind = MessageKind.Seed;

        Assert.That(kind, Is.EqualTo(MessageKind.Seed));
    }

    [Test]
    public void MessageKind_HasResponseValue()
    {
        MessageKind kind = MessageKind.Response;

        Assert.That(kind, Is.EqualTo(MessageKind.Response));
    }

    [Test]
    public void MessageKind_HasThinkingValue()
    {
        MessageKind kind = MessageKind.Thinking;

        Assert.That(kind, Is.EqualTo(MessageKind.Thinking));
    }

    [Test]
    public void MessageKind_HasHeartbeatValue()
    {
        MessageKind kind = MessageKind.Heartbeat;

        Assert.That(kind, Is.EqualTo(MessageKind.Heartbeat));
    }
}
