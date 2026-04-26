using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using ModelDebate.Access.Chat.Interface;
using ModelDebate.Access.Chat.Service.OpenAI;
using NUnit.Framework;

namespace Test.Unit.Access.Chat;

[TestFixture]
public class OpenAiChatServiceSanity
{
    #region Members

    private OpenAiChatService m_Service;

    #endregion

    #region C'tor / Setup

    [SetUp]
    public void SetUp()
    {
        string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? throw new InvalidOperationException(
                "OPENAI_API_KEY environment variable is not set. " +
                "Set it to a valid OpenAI API key before running these sanity tests.");

        m_Service = new OpenAiChatService(apiKey, "gpt-4o", NullLogger<OpenAiChatService>.Instance);
    }

    #endregion

    #region Public

    [Test]
    public async Task GivenValidRequest_WhenCompleteAsync_ThenResponseHasContent()
    {
        ChatRequest request = new ChatRequest(
            "You are a helpful assistant. Reply in exactly one sentence.",
            "Say hello.");

        ChatResponse response = await m_Service.CompleteAsync(request, CancellationToken.None);

        Assert.That(response,         Is.Not.Null);
        Assert.That(response.Content, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task GivenValidRequest_WhenCompleteAsync_ThenMetadataPopulated()
    {
        ChatRequest request = new ChatRequest(
            "You are a helpful assistant.",
            "What is 2 plus 2? Answer in one word.");

        ChatResponse response = await m_Service.CompleteAsync(request, CancellationToken.None);

        Assert.That(response.Metadata,              Is.Not.Null);
        Assert.That(response.Metadata.Provider,     Is.EqualTo("OpenAI"));
        Assert.That(response.Metadata.ModelId,      Is.Not.Null.And.Not.Empty);
        Assert.That(response.Metadata.InputTokens,  Is.GreaterThan(0));
        Assert.That(response.Metadata.OutputTokens, Is.GreaterThan(0));
        Assert.That(response.Metadata.Latency,      Is.GreaterThan(TimeSpan.Zero));
    }

    #endregion
}
