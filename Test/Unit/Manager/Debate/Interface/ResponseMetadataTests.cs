using System;
using ModelDebate.Manager.Debate.Interface;
using NUnit.Framework;

namespace Test.Unit.Manager.Debate;

[TestFixture]
public class ResponseMetadataTests
{
    [Test]
    public void Constructor_SetsProvider()
    {
        TimeSpan        latency  = TimeSpan.FromMilliseconds(300);
        ResponseMetadata metadata = new ResponseMetadata("anthropic", "claude-3", "3.0", 50, 100, latency);

        Assert.That(metadata.Provider, Is.EqualTo("anthropic"));
    }

    [Test]
    public void Constructor_SetsModelId()
    {
        TimeSpan         latency  = TimeSpan.FromMilliseconds(300);
        ResponseMetadata metadata = new ResponseMetadata("anthropic", "claude-3", "3.0", 50, 100, latency);

        Assert.That(metadata.ModelId, Is.EqualTo("claude-3"));
    }

    [Test]
    public void Constructor_SetsModelVersion()
    {
        TimeSpan         latency  = TimeSpan.FromMilliseconds(300);
        ResponseMetadata metadata = new ResponseMetadata("anthropic", "claude-3", "3.0", 50, 100, latency);

        Assert.That(metadata.ModelVersion, Is.EqualTo("3.0"));
    }

    [Test]
    public void Constructor_SetsInputTokens()
    {
        TimeSpan         latency  = TimeSpan.FromMilliseconds(300);
        ResponseMetadata metadata = new ResponseMetadata("anthropic", "claude-3", "3.0", 50, 100, latency);

        Assert.That(metadata.InputTokens, Is.EqualTo(50));
    }

    [Test]
    public void Constructor_SetsOutputTokens()
    {
        TimeSpan         latency  = TimeSpan.FromMilliseconds(300);
        ResponseMetadata metadata = new ResponseMetadata("anthropic", "claude-3", "3.0", 50, 100, latency);

        Assert.That(metadata.OutputTokens, Is.EqualTo(100));
    }

    [Test]
    public void Constructor_SetsLatency()
    {
        TimeSpan         latency  = TimeSpan.FromMilliseconds(300);
        ResponseMetadata metadata = new ResponseMetadata("anthropic", "claude-3", "3.0", 50, 100, latency);

        Assert.That(metadata.Latency, Is.EqualTo(latency));
    }
}
