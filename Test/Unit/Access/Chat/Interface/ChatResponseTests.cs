using ModelDebate.Access.Chat.Interface;
using NUnit.Framework;

namespace Test.Unit.Access.Chat;

[TestFixture]
public class ChatResponseTests
{
    [Test]
    public void Constructor_SetsContent()
    {
        ModelMetadata metadata = new ModelMetadata("provider", "id", "1.0", 10, 5, System.TimeSpan.Zero);
        ChatResponse  response = new ChatResponse("hello", metadata);

        Assert.That(response.Content, Is.EqualTo("hello"));
    }

    [Test]
    public void Constructor_SetsMetadata()
    {
        ModelMetadata metadata = new ModelMetadata("provider", "id", "1.0", 10, 5, System.TimeSpan.Zero);
        ChatResponse  response = new ChatResponse("hello", metadata);

        Assert.That(response.Metadata, Is.SameAs(metadata));
    }
}
