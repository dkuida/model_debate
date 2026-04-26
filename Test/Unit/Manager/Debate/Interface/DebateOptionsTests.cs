using ModelDebate.Manager.Debate.Interface;
using NUnit.Framework;

namespace Test.Unit.Manager.Debate;

[TestFixture]
public class DebateOptionsTests
{
    [Test]
    public void Constructor_SetsAnthropicModel()
    {
        DebateOptions options = new DebateOptions("claude-3-5-sonnet", "gpt-4o", 30, "/logs");

        Assert.That(options.AnthropicModel, Is.EqualTo("claude-3-5-sonnet"));
    }

    [Test]
    public void Constructor_SetsOpenAiModel()
    {
        DebateOptions options = new DebateOptions("claude-3-5-sonnet", "gpt-4o", 30, "/logs");

        Assert.That(options.OpenAiModel, Is.EqualTo("gpt-4o"));
    }

    [Test]
    public void Constructor_SetsTurnTimeoutSeconds()
    {
        DebateOptions options = new DebateOptions("claude-3-5-sonnet", "gpt-4o", 30, "/logs");

        Assert.That(options.TurnTimeoutSeconds, Is.EqualTo(30));
    }

    [Test]
    public void Constructor_SetsLogDirectory()
    {
        DebateOptions options = new DebateOptions("claude-3-5-sonnet", "gpt-4o", 30, "/logs");

        Assert.That(options.LogDirectory, Is.EqualTo("/logs"));
    }
}
