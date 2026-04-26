using System;
using ModelDebate.Manager.Debate.Interface;
using NUnit.Framework;

namespace Test.Unit.Manager.Debate;

[TestFixture]
public class DebateResponseTests
{
    private static ResponseMetadata MakeMetadata()
    {
        return new ResponseMetadata("openai", "gpt-4", "4.0", 10, 20, TimeSpan.FromMilliseconds(100));
    }

    [Test]
    public void Constructor_SetsFromParticipant()
    {
        DebateResponse response = new DebateResponse("Claude", "Hello!", MakeMetadata());

        Assert.That(response.FromParticipant, Is.EqualTo("Claude"));
    }

    [Test]
    public void Constructor_SetsContent()
    {
        DebateResponse response = new DebateResponse("Claude", "Hello!", MakeMetadata());

        Assert.That(response.Content, Is.EqualTo("Hello!"));
    }

    [Test]
    public void Constructor_SetsMetadata()
    {
        ResponseMetadata metadata = MakeMetadata();
        DebateResponse   response = new DebateResponse("Claude", "Hello!", metadata);

        Assert.That(response.Metadata, Is.SameAs(metadata));
    }

    [Test]
    public void Constructor_MessageId_IsThirtyTwoCharHexString()
    {
        DebateResponse response = new DebateResponse("Claude", "Hello!", MakeMetadata());

        Assert.That(response.MessageId, Has.Length.EqualTo(32));
        Assert.That(response.MessageId, Does.Match("^[0-9a-f]{32}$"));
    }

    [Test]
    public void Constructor_SentAt_IsCloseToUtcNow()
    {
        DateTimeOffset before   = DateTimeOffset.UtcNow;
        DebateResponse response = new DebateResponse("Claude", "Hello!", MakeMetadata());
        DateTimeOffset after    = DateTimeOffset.UtcNow;

        Assert.That(response.SentAt, Is.GreaterThanOrEqualTo(before));
        Assert.That(response.SentAt, Is.LessThanOrEqualTo(after));
    }

    [Test]
    public void Kind_IsResponse()
    {
        DebateResponse response = new DebateResponse("Claude", "Hello!", MakeMetadata());

        Assert.That(response.Kind, Is.EqualTo(MessageKind.Response));
    }

    [Test]
    public void ImplementsIDebateMessage()
    {
        DebateResponse response = new DebateResponse("Claude", "Hello!", MakeMetadata());

        Assert.That(response, Is.InstanceOf<IDebateMessage>());
    }
}
