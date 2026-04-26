using System.Threading.Channels;
using ModelDebate.iFx.Utilities;
using NUnit.Framework;

namespace Test.Unit.iFX.Utilities;

[TestFixture]
public class DebateChannelTests
{
    [Test]
    public void Constructor_ClaudeInbox_IsNotNull()
    {
        DebateChannel<string> channel = new DebateChannel<string>();

        Assert.That(channel.ClaudeInbox, Is.Not.Null);
    }

    [Test]
    public void Constructor_GptInbox_IsNotNull()
    {
        DebateChannel<string> channel = new DebateChannel<string>();

        Assert.That(channel.GptInbox, Is.Not.Null);
    }

    [Test]
    public void Constructor_ClaudeInbox_IsChannel()
    {
        DebateChannel<string> channel = new DebateChannel<string>();

        Assert.That(channel.ClaudeInbox, Is.InstanceOf<Channel<string>>());
    }

    [Test]
    public void Constructor_GptInbox_IsChannel()
    {
        DebateChannel<string> channel = new DebateChannel<string>();

        Assert.That(channel.GptInbox, Is.InstanceOf<Channel<string>>());
    }

    [Test]
    public void Constructor_ClaudeAndGptInbox_AreDistinctChannels()
    {
        DebateChannel<string> channel = new DebateChannel<string>();

        Assert.That(channel.ClaudeInbox, Is.Not.SameAs(channel.GptInbox));
    }
}
