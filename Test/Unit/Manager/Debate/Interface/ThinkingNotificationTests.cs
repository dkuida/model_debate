using System;
using ModelDebate.Manager.Debate.Interface;
using NUnit.Framework;

namespace Test.Unit.Manager.Debate;

[TestFixture]
public class ThinkingNotificationTests
{
    [Test]
    public void Constructor_SetsFromParticipant()
    {
        ThinkingNotification notification = new ThinkingNotification("GPT");

        Assert.That(notification.FromParticipant, Is.EqualTo("GPT"));
    }

    [Test]
    public void Constructor_MessageId_IsThirtyTwoCharHexString()
    {
        ThinkingNotification notification = new ThinkingNotification("GPT");

        Assert.That(notification.MessageId, Has.Length.EqualTo(32));
        Assert.That(notification.MessageId, Does.Match("^[0-9a-f]{32}$"));
    }

    [Test]
    public void Constructor_SentAt_IsCloseToUtcNow()
    {
        DateTimeOffset       before       = DateTimeOffset.UtcNow;
        ThinkingNotification notification = new ThinkingNotification("GPT");
        DateTimeOffset       after        = DateTimeOffset.UtcNow;

        Assert.That(notification.SentAt, Is.GreaterThanOrEqualTo(before));
        Assert.That(notification.SentAt, Is.LessThanOrEqualTo(after));
    }

    [Test]
    public void Kind_IsThinking()
    {
        ThinkingNotification notification = new ThinkingNotification("GPT");

        Assert.That(notification.Kind, Is.EqualTo(MessageKind.Thinking));
    }

    [Test]
    public void ImplementsIDebateMessage()
    {
        ThinkingNotification notification = new ThinkingNotification("GPT");

        Assert.That(notification, Is.InstanceOf<IDebateMessage>());
    }
}
