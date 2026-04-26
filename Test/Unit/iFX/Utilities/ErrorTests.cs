using System;
using ModelDebate.iFx.Utilities;
using NUnit.Framework;

namespace Test.Unit.iFX.Utilities;

[TestFixture]
public class ErrorTests
{
    [Test]
    public void Constructor_WithCodeAndDescription_SetsProperties()
    {
        Error error = new Error("E001", "Something went wrong");

        Assert.That(error.Code,        Is.EqualTo("E001"));
        Assert.That(error.Description, Is.EqualTo("Something went wrong"));
    }

    [Test]
    public void Constructor_WithException_SetsCodeToException()
    {
        Exception exception = new("test message");

        Error error = new Error(exception);

        Assert.That(error.Code, Is.EqualTo("Exception"));
    }

    [Test]
    public void Constructor_WithException_SetsDescriptionWithMessageAndStackTrace()
    {
        Exception exception = new Exception("test message");

        Error error = new Error(exception);

        Assert.That(error.Description, Does.Contain("Message: test message"));
        Assert.That(error.Description, Does.Contain("StackTrace:"));
    }

    [Test]
    public void ToString_ReturnsFormattedString()
    {
        Error error = new Error("E999", "test description");

        string result = error.ToString();

        Assert.That(result, Is.EqualTo("[E999] test description"));
    }
}
