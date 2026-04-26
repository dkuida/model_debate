using System;
using ModelDebate.Manager.Debate.Interface;
using NUnit.Framework;

namespace Test.Unit.Manager.Debate;

[TestFixture]
public class SeedMessageTests
{
    [Test]
    public void Constructor_SetsTopic()
    {
        SeedMessage msg = new SeedMessage("AI Ethics");

        Assert.That(msg.Topic, Is.EqualTo("AI Ethics"));
    }

    [Test]
    public void Constructor_DefaultFromParticipant_IsUser()
    {
        SeedMessage msg = new SeedMessage("AI Ethics");

        Assert.That(msg.FromParticipant, Is.EqualTo("User"));
    }

    [Test]
    public void Constructor_ExplicitFromParticipant_IsSet()
    {
        SeedMessage msg = new SeedMessage("AI Ethics", "Moderator");

        Assert.That(msg.FromParticipant, Is.EqualTo("Moderator"));
    }

    [Test]
    public void Constructor_MessageId_IsThirtyTwoCharHexString()
    {
        SeedMessage msg = new SeedMessage("AI Ethics");

        Assert.That(msg.MessageId, Has.Length.EqualTo(32));
        Assert.That(msg.MessageId, Does.Match("^[0-9a-f]{32}$"));
    }

    [Test]
    public void Constructor_SentAt_IsCloseToUtcNow()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow;
        SeedMessage    msg    = new SeedMessage("AI Ethics");
        DateTimeOffset after  = DateTimeOffset.UtcNow;

        Assert.That(msg.SentAt, Is.GreaterThanOrEqualTo(before));
        Assert.That(msg.SentAt, Is.LessThanOrEqualTo(after));
    }

    [Test]
    public void Kind_IsSeed()
    {
        SeedMessage msg = new SeedMessage("AI Ethics");

        Assert.That(msg.Kind, Is.EqualTo(MessageKind.Seed));
    }

    [Test]
    public void ImplementsIDebateMessage()
    {
        SeedMessage msg = new SeedMessage("AI Ethics");

        Assert.That(msg, Is.InstanceOf<IDebateMessage>());
    }
}
