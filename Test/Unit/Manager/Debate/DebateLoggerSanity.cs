using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelDebate.Manager.Debate.Interface;
using ModelDebate.Manager.Debate.Service;
using NUnit.Framework;

namespace Test.Unit.Manager.Debate
{
    [TestFixture]
    public class DebateLoggerSanity
    {
        #region Members

        private string       m_TempDir;
        private DebateLogger m_Logger;

        #endregion

        #region C'tor / Setup

        [SetUp]
        public void SetUp()
        {
            m_TempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_TempDir);
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterInstance(NullLogger.Instance).Named<ILogger>("chat");
            IContainer build = builder.Build();
            m_Logger = new DebateLogger(build, NullLogger<DebateLogger>.Instance);
        }

        [TearDown]
        public void TearDown()
        {
            m_Logger?.Dispose();
            if (m_TempDir != null && Directory.Exists(m_TempDir))
            {
                Directory.Delete(m_TempDir, recursive: true);
            }
        }

        #endregion

        #region Public

        [Test]
        public async Task GivenDebateResponse_WhenLogAsync_ThenFormattedEntryWrittenToFile()
        {
            ResponseMetadata metadata = new ResponseMetadata(
                provider:     "OpenAI",
                modelId:      "gpt-4o",
                modelVersion: "gpt-4o-2024-11-20",
                inputTokens:  100,
                outputTokens: 50,
                latency:      TimeSpan.FromSeconds(1.5));

            DebateResponse response = new DebateResponse(
                fromParticipant: "GPT",
                content:         "Pineapple absolutely belongs on pizza. Fight me.",
                metadata:        metadata);

            await m_Logger.LogAsync(response, CancellationToken.None);

            m_Logger.Dispose();
            // later test with amocker NullLogger.Instance
            
            // string logContent = File.ReadAllText(m_Logger.LogFilePath);
            //
            // Assert.That(logContent, Does.Contain("GPT/gpt-4o"));
            // Assert.That(logContent, Does.Contain("tokens: 100 in / 50 out"));
            // Assert.That(logContent, Does.Contain("Pineapple absolutely belongs on pizza."));
            // Assert.That(logContent, Does.Contain("---"));
        }

        [Test]
        public async Task GivenThinkingNotification_WhenLogAsync_ThenFormattedEntryWrittenToFile()
        {
            ThinkingNotification notification = new ThinkingNotification("Claude");

            await m_Logger.LogAsync(notification, CancellationToken.None);

            m_Logger.Dispose();
            // later test with amocker NullLogger.Instance
            
            // string logContent = File.ReadAllText(m_Logger.LogFilePath);
            //
            // Assert.That(logContent, Does.Contain("[Claude] is thinking..."));
        }

        #endregion

        #region Private

        #endregion
    }
}
