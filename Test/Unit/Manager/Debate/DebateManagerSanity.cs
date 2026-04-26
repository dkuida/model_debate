using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using ModelDebate.Access.Chat.Interface;
using ModelDebate.Manager.Debate.Interface;
using ModelDebate.Manager.Debate.Service;
using NUnit.Framework;

namespace Test.Unit.Manager.Debate
{
    [TestFixture]
    public class DebateManagerSanity
    {
        #region Members

        private static readonly string s_TempDir = Path.GetTempPath();

        #endregion

        #region Public

        [Test]
        public async Task GivenTopic_WhenSeed_ThenClaudeReceivesFirstMessage()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();

            FakeChatResource gptResource = new FakeChatResource(
                response:            FakeResponses.Make("OpenAI", "gpt-4o", "GPT opening argument."),
                maxCallsBeforeCancel: 1,
                ctsToCancel:         cts);
            FakeChatResource claudeResource = new FakeChatResource(
                response:            FakeResponses.Make("Anthropic", "claude-3-5-sonnet", "Claude response."),
                maxCallsBeforeCancel: 1,
                ctsToCancel:         cts);
            FakeDebateLogger logger  = new FakeDebateLogger();
            DebateOptions    options = new DebateOptions("claude-3-5-sonnet-20241022", "gpt-4o", 30, s_TempDir);

            DebateManager manager = new DebateManager(claudeResource, gptResource, logger, options,
                    NullLogger<DebateManager>.Instance);
            SeedMessage   seed    = new SeedMessage("Is pineapple on pizza acceptable?");

            try
            {
                await manager.RunAsync(seed, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // expected
            }

            Assert.That(claudeResource.ReceivedRequests.Count,          Is.GreaterThanOrEqualTo(1));
            Assert.That(claudeResource.ReceivedRequests[0].UserMessage, Does.Contain(seed.Topic));
        }

        [Test]
        public async Task GivenTwoTurns_WhenRun_ThenBothParticipantsRespond()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();

            FakeChatResource gptResource = new FakeChatResource(
                response:            FakeResponses.Make("OpenAI", "gpt-4o", "GPT says yes."),
                maxCallsBeforeCancel: 1,
                ctsToCancel:         cts);

            FakeChatResource claudeResource = new FakeChatResource(
                response: FakeResponses.Make("Anthropic", "claude-3-5-sonnet", "Claude says no."));

            FakeDebateLogger logger  = new FakeDebateLogger();
            DebateOptions    options = new DebateOptions("claude-3-5-sonnet-20241022", "gpt-4o", 30, s_TempDir);

            DebateManager manager = new DebateManager(claudeResource, gptResource, logger, options,
                    NullLogger<DebateManager>.Instance);

            try
            {
                await manager.RunAsync(new SeedMessage("Remote work vs office?"), cts.Token);
            }
            catch (OperationCanceledException) { }

            Assert.That(gptResource.ReceivedRequests.Count,   Is.EqualTo(1), "GPT called exactly once");
            Assert.That(claudeResource.ReceivedRequests.Count, Is.EqualTo(1), "Claude called exactly once");

            System.Collections.Generic.IEnumerable<DebateResponse> responses =
                logger.LoggedMessages.OfType<DebateResponse>();

            Assert.That(responses.Count(), Is.EqualTo(2), "2 DebateResponses logged");
        }

        [Test]
        public async Task GivenTwoTurns_WhenRun_ThenClaudeReceivesGptResponseAsNextMessage()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();

            string gptContent = "GPT's unique argument content.";

            FakeChatResource claudeResource = new FakeChatResource(
                response:            FakeResponses.Make("Anthropic", "claude-3-5-sonnet", "Claude first reply."),
                maxCallsBeforeCancel: 2,
                ctsToCancel:         cts);

            FakeChatResource gptResource = new FakeChatResource(
                response: FakeResponses.Make("OpenAI", "gpt-4o", gptContent));

            FakeDebateLogger logger  = new FakeDebateLogger();
            DebateOptions    options = new DebateOptions("claude-3-5-sonnet-20241022", "gpt-4o", 30, s_TempDir);
            DebateManager    manager = new DebateManager(claudeResource, gptResource, logger, options,
                    NullLogger<DebateManager>.Instance);

            try
            {
                await manager.RunAsync(new SeedMessage("Tabs vs spaces?"), cts.Token);
            }
            catch (OperationCanceledException) { }

            Assert.That(claudeResource.ReceivedRequests.Count,          Is.GreaterThanOrEqualTo(2));
            Assert.That(claudeResource.ReceivedRequests[1].UserMessage, Is.EqualTo(gptContent));
        }

        [Test]
        public async Task GivenChatResponse_WhenMapped_ThenDebateResponseContainsCorrectMetadata()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();

            ModelMetadata    accessMeta    = new ModelMetadata(
                provider:     "Anthropic",
                modelId:      "claude-3-5-sonnet-20241022",
                modelVersion: "claude-3-5-sonnet-20241022",
                inputTokens:  100,
                outputTokens: 50,
                latency:      TimeSpan.FromSeconds(2.0));

            ChatResponse     chatResponse  = new ChatResponse("Mapped content.", accessMeta);
            FakeChatResource claudeResource = new FakeChatResource(chatResponse,
                maxCallsBeforeCancel: 1, ctsToCancel: cts);
            FakeChatResource gptResource    = new FakeChatResource(
                FakeResponses.Make("OpenAI", "gpt-4o"));
            FakeDebateLogger logger  = new FakeDebateLogger();
            DebateOptions    options = new DebateOptions("claude", "gpt-4o", 30, s_TempDir);
            DebateManager    manager = new DebateManager(claudeResource, gptResource, logger, options,
                    NullLogger<DebateManager>.Instance);

            try
            {
                await manager.RunAsync(new SeedMessage("Does mapping work?"), cts.Token);
            }
            catch (OperationCanceledException) { }

            DebateResponse? debateResp = logger.LoggedMessages.OfType<DebateResponse>().FirstOrDefault();

            Assert.That(debateResp,                         Is.Not.Null,                    "DebateResponse logged");
            Assert.That(debateResp!.Metadata.Provider,      Is.EqualTo("Anthropic"),                "provider mapped");
            Assert.That(debateResp.Metadata.ModelId,        Is.EqualTo("claude-3-5-sonnet-20241022"), "modelId mapped");
            Assert.That(debateResp.Metadata.InputTokens,    Is.EqualTo(100),                "inputTokens mapped");
            Assert.That(debateResp.Metadata.OutputTokens,   Is.EqualTo(50),                 "outputTokens mapped");
            Assert.That(debateResp.Metadata.Latency,        Is.EqualTo(TimeSpan.FromSeconds(2.0)), "latency mapped");
            Assert.That(debateResp.Content,                 Is.EqualTo("Mapped content."),  "content preserved");
        }

        [Test]
        public async Task GivenSlowParticipant_WhenTurnExceedsTimeout_ThenDebateEndsCleanly()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();

            FakeChatResource claudeResource = new FakeChatResource(
                response: FakeResponses.Make("Anthropic", "claude-3-5-sonnet"),
                delay:    TimeSpan.FromSeconds(10));

            FakeChatResource gptResource = new FakeChatResource(
                FakeResponses.Make("OpenAI", "gpt-4o"));

            FakeDebateLogger logger  = new FakeDebateLogger();
            DebateOptions    options = new DebateOptions(
                anthropicModel:     "claude-3-5-sonnet-20241022",
                openAiModel:        "gpt-4o",
                turnTimeoutSeconds: 1,
                logDirectory:       s_TempDir);

            DebateManager manager = new DebateManager(claudeResource, gptResource, logger, options,
                    NullLogger<DebateManager>.Instance);

            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            await manager.RunAsync(new SeedMessage("Is async the future?"), cts.Token);
            sw.Stop();

            Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(3)), "debate ended quickly after timeout");

            bool timeoutLogged = false;
            foreach (IDebateMessage msg in logger.LoggedMessages)
            {
                if (msg is ThinkingNotification t && t.FromParticipant.Contains("Timeout", StringComparison.Ordinal))
                {
                    timeoutLogged = true;
                    break;
                }
            }
            
            Assert.That(timeoutLogged, Is.True, "timeout notification logged");
        }

        [Test]
        public async Task GivenApiFailed_WhenCompleteAsync_ThenDebateEndsAndErrorLogged()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();

            FakeChatResource claudeResource = new FakeChatResource(
                response:    FakeResponses.Make("Anthropic", "claude-3-5-sonnet"),
                shouldThrow: true);

            FakeChatResource gptResource = new FakeChatResource(
                FakeResponses.Make("OpenAI", "gpt-4o"));

            FakeDebateLogger logger  = new FakeDebateLogger();
            DebateOptions    options = new DebateOptions("claude-3-5-sonnet-20241022", "gpt-4o", 30, s_TempDir);

            DebateManager manager = new DebateManager(claudeResource, gptResource, logger, options,
                    NullLogger<DebateManager>.Instance);

            await manager.RunAsync(new SeedMessage("Does error handling work?"), cts.Token);

            bool errorLogged = false;
            foreach (IDebateMessage msg in logger.LoggedMessages)
            {
                if (msg is ThinkingNotification t && t.FromParticipant.Contains("Error", StringComparison.Ordinal))
                {
                    errorLogged = true;
                    break;
                }
            }
            
            Assert.That(errorLogged, Is.True, "error notification logged");
        }

        [Test]
        public async Task GivenMaxTurns_WhenLimitReached_ThenDebateEndsAfterExactlyMaxTurns()
        {
            // Arrange — no cancellation from fakes; only MaxTurns stops the loop
            using CancellationTokenSource cts = new CancellationTokenSource();

            FakeChatResource claudeResource = new FakeChatResource(
                FakeResponses.Make("Anthropic", "claude-3-5-sonnet", "Claude turn."));
            FakeChatResource gptResource    = new FakeChatResource(
                FakeResponses.Make("OpenAI", "gpt-4o", "GPT turn."));
            FakeDebateLogger logger  = new FakeDebateLogger();
            DebateOptions    options = new DebateOptions(
                anthropicModel:     "claude-3-5-sonnet-20241022",
                openAiModel:        "gpt-4o",
                turnTimeoutSeconds: 30,
                logDirectory:       s_TempDir,
                maxTurns:           4);   // exactly 4 total turns: Claude, GPT, Claude, GPT

            DebateManager manager = new DebateManager(claudeResource, gptResource, logger, options,
                NullLogger<DebateManager>.Instance);
            SeedMessage seed = new SeedMessage("Is 4 a good number of turns?");

            // Act — RunAsync should return naturally (no cancellation needed)
            await manager.RunAsync(seed, cts.Token);

            // Assert — exactly 4 total LLM calls: 2 Claude + 2 GPT
            Assert.That(claudeResource.ReceivedRequests.Count, Is.EqualTo(2), "Claude called twice");
            Assert.That(gptResource.ReceivedRequests.Count,    Is.EqualTo(2), "GPT called twice");

            System.Collections.Generic.IEnumerable<DebateResponse> responses =
                logger.LoggedMessages.OfType<DebateResponse>();

            Assert.That(responses.Count(), Is.EqualTo(4), "4 DebateResponses logged total");
        }

        #endregion

        #region Private

        #endregion
    }
}