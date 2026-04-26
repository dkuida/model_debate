using ModelDebate.iFx.Utilities;
using NUnit.Framework;

namespace Test.Unit.iFX.Utilities;

[TestFixture]
public class ErrorMessageTests
{
    [Test]
    public void ApiKeyMissing_HasExpectedValue()
    {
        Assert.That(ErrorMessage.ApiKeyMissing, Is.EqualTo("API key not configured."));
    }

    [Test]
    public void TurnTimeout_HasExpectedValue()
    {
        Assert.That(ErrorMessage.TurnTimeout, Is.EqualTo("Turn timed out with no response."));
    }

    [Test]
    public void ApiFailed_HasExpectedValue()
    {
        Assert.That(ErrorMessage.ApiFailed, Is.EqualTo("LLM API call failed."));
    }
}
