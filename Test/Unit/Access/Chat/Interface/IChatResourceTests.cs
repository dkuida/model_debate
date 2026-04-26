using System.Threading;
using System.Threading.Tasks;
using ModelDebate.Access.Chat.Interface;
using NUnit.Framework;

namespace Test.Unit.Access.Chat;

[TestFixture]
public class ChatResourceTests
{
    private sealed class StubChatResource : IChatResource
    {
        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct)
        {
            ModelMetadata metadata = new ModelMetadata("stub", "stub-id", "1.0", 0, 0, System.TimeSpan.Zero);
            ChatResponse  response = new ChatResponse("stub-response", metadata);
            return Task.FromResult(response);
        }
    }

    [Test]
    public async Task CompleteAsync_ReturnsResponse()
    {
        IChatResource resource = new StubChatResource();
        ChatRequest   request  = new ChatRequest("system", "user");

        ChatResponse response = await resource.CompleteAsync(request, CancellationToken.None);

        Assert.That(response, Is.Not.Null);
    }

    [Test]
    public async Task CompleteAsync_ReturnsResponseWithContent()
    {
        IChatResource resource = new StubChatResource();
        ChatRequest   request  = new ChatRequest("system", "user");

        ChatResponse response = await resource.CompleteAsync(request, CancellationToken.None);

        Assert.That(response.Content, Is.Not.Null);
    }
}
