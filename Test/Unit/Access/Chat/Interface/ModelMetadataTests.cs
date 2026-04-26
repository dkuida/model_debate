using System;
using ModelDebate.Access.Chat.Interface;
using NUnit.Framework;

namespace Test.Unit.Access.Chat;

[TestFixture]
public class ModelMetadataTests
{
    [Test]
    public void Constructor_SetsProvider()
    {
        TimeSpan      latency  = TimeSpan.FromMilliseconds(500);
        ModelMetadata metadata = new ModelMetadata("openai", "gpt-4", "4.0", 100, 50, latency);

        Assert.That(metadata.Provider, Is.EqualTo("openai"));
    }

    [Test]
    public void Constructor_SetsModelId()
    {
        TimeSpan      latency  = TimeSpan.FromMilliseconds(500);
        ModelMetadata metadata = new ModelMetadata("openai", "gpt-4", "4.0", 100, 50, latency);

        Assert.That(metadata.ModelId, Is.EqualTo("gpt-4"));
    }

    [Test]
    public void Constructor_SetsModelVersion()
    {
        TimeSpan      latency  = TimeSpan.FromMilliseconds(500);
        ModelMetadata metadata = new ModelMetadata("openai", "gpt-4", "4.0", 100, 50, latency);

        Assert.That(metadata.ModelVersion, Is.EqualTo("4.0"));
    }

    [Test]
    public void Constructor_SetsInputTokens()
    {
        TimeSpan      latency  = TimeSpan.FromMilliseconds(500);
        ModelMetadata metadata = new ModelMetadata("openai", "gpt-4", "4.0", 100, 50, latency);

        Assert.That(metadata.InputTokens, Is.EqualTo(100));
    }

    [Test]
    public void Constructor_SetsOutputTokens()
    {
        TimeSpan      latency  = TimeSpan.FromMilliseconds(500);
        ModelMetadata metadata = new ModelMetadata("openai", "gpt-4", "4.0", 100, 50, latency);

        Assert.That(metadata.OutputTokens, Is.EqualTo(50));
    }

    [Test]
    public void Constructor_SetsLatency()
    {
        TimeSpan      latency  = TimeSpan.FromMilliseconds(500);
        ModelMetadata metadata = new ModelMetadata("openai", "gpt-4", "4.0", 100, 50, latency);

        Assert.That(metadata.Latency, Is.EqualTo(latency));
    }
}
