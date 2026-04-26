using ModelDebate.Access.Chat.Interface;
using NUnit.Framework;

namespace Test.Unit.Access.Chat;

[TestFixture]
public class ChatRequestTests
{
    [Test]
    public void Constructor_SetsSystemPrompt()
    {
        ChatRequest request = new ChatRequest("sys", "msg");

        Assert.That(request.SystemPrompt, Is.EqualTo("sys"));
    }

    [Test]
    public void Constructor_SetsUserMessage()
    {
        ChatRequest request = new ChatRequest("sys", "msg");

        Assert.That(request.UserMessage, Is.EqualTo("msg"));
    }
}
