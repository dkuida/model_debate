using System;
using ModelDebate.Manager.Debate.Interface;
using NUnit.Framework;

namespace Test.Unit.Manager.Debate;

[TestFixture]
public class HeartbeatPingTests
{
    [Test]
    public void Constructor_SetsFromParticipant()
    {
        HeartbeatPing ping = new HeartbeatPing("Claude");

        Assert.That(ping.FromParticipant, Is.EqualTo("Claude"));
    }

    [Test]
    public void Constructor_MessageId_IsThirtyTwoCharHexString()
    {
        HeartbeatPing ping = new HeartbeatPing("Claude");

        Assert.That(ping.MessageId, Has.Length.EqualTo(32));
        Assert.That(ping.MessageId, Does.Match("^[0-9a-f]{32}$"));
    }

    [Test]
    public void Constructor_SentAt_IsCloseToUtcNow()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow;
        HeartbeatPing  ping   = new HeartbeatPing("Claude");
        DateTimeOffset after  = DateTimeOffset.UtcNow;

        Assert.That(ping.SentAt, Is.GreaterThanOrEqualTo(before));
        Assert.That(ping.SentAt, Is.LessThanOrEqualTo(after));
    }

    [Test]
    public void Kind_IsHeartbeat()
    {
        HeartbeatPing ping = new HeartbeatPing("Claude");

        Assert.That(ping.Kind, Is.EqualTo(MessageKind.Heartbeat));
    }

    [Test]
    public void ImplementsIDebateMessage()
    {
        HeartbeatPing ping = new HeartbeatPing("Claude");

        Assert.That(ping, Is.InstanceOf<IDebateMessage>());
    }
}
