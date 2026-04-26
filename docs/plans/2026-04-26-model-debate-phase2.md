# Model Debate — Phase 2: Manager + Entry Point

> **For execution:** Use `/execute-plan` mode or the subagent-driven-development recipe.
> **Prerequisite:** Phase 1 must be complete — all contracts established, both Access services passing tests.

**Goal:** `DebateLogger` + `DebateManager` (full orchestration with explicit boundary mapping) + `Program.cs` composition root + smoke verification.

**Architecture:** `DebateManager` is the single orchestrator — it owns the turn loop, timeouts, logging, and shutdown. At every Access→Manager boundary, an explicit private `Map*()` method converts between the two type systems. Participants (`IChatResource`) receive only `ChatRequest` and return `ChatResponse` — they never see `IDebateMessage`. The `DebateManager` maps those Access types to Manager types before handing them off to `IDebateLogger`.

**Tech Stack:** .NET 10, C# 12, System.Threading.Channels (iFX), Serilog (NuGet — not used directly in DebateLogger; StreamWriter used for simplicity), NUnit 4

---

## Boundary Mapping Rules (enforce throughout)

```
DebateManager only passes to IChatResource:     ChatRequest    (Access type)
DebateManager only receives from IChatResource: ChatResponse   (Access type)

At the boundary — INSIDE DebateManager:
  MapToRequest(string systemPrompt, string userMessage) → ChatRequest
  MapToDebateResponse(string speaker, ChatResponse)     → DebateResponse

DebateManager only passes to IDebateLogger:  IDebateMessage   (Manager type)
DebateManager never passes ChatResponse to anyone else.
```

---

## Task 1: RED — DebateLogger tests

**Files to create:**
- `Test/Unit/Manager/Debate/DebateLoggerSanity.cs`

> `DebateLogger` implements `IDebateLogger` and `IDisposable`. The test creates a temp directory, writes messages, then disposes the logger before reading the file (to ensure the `StreamWriter` is flushed). The `Test.Unit.Manager.Debate` project was created in Phase 1.

**Step 1: Write the failing test**

`Test/Unit/Manager/Debate/DebateLoggerSanity.cs`:

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
            m_Logger = new DebateLogger(m_TempDir);
        }

        [TearDown]
        public void TearDown()
        {
            m_Logger.Dispose();  // flush StreamWriter before reading file or deleting dir
            Directory.Delete(m_TempDir, recursive: true);
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

            m_Logger.Dispose();  // flush before reading
            string logContent = File.ReadAllText(m_Logger.LogFilePath);

            Assert.That(logContent, Does.Contain("GPT/gpt-4o"));
            Assert.That(logContent, Does.Contain("tokens: 100 in / 50 out"));
            Assert.That(logContent, Does.Contain("Pineapple absolutely belongs on pizza."));
            Assert.That(logContent, Does.Contain("---"));
        }

        [Test]
        public async Task GivenThinkingNotification_WhenLogAsync_ThenFormattedEntryWrittenToFile()
        {
            ThinkingNotification notification = new ThinkingNotification("Claude");

            await m_Logger.LogAsync(notification, CancellationToken.None);

            m_Logger.Dispose();
            string logContent = File.ReadAllText(m_Logger.LogFilePath);

            Assert.That(logContent, Does.Contain("[Claude] is thinking..."));
        }

        #endregion

        #region Private

        #endregion
    }
}
```

**Step 2: Run the tests to verify they FAIL**

```bash
cd /home/dkuida/code/model_debate
dotnet test Test/Unit/Manager/Debate/Test.Unit.Manager.Debate.csproj \
    --filter "DebateLoggerSanity" -v normal
```

Expected: FAIL — `The type or namespace name 'DebateLogger' could not be found`

---

## Task 2: GREEN — DebateLogger implementation

**Files to create:**
- `Manager/Debate/Service/DebateLogger.cs`

**Step 1: Write the implementation**

> Uses `StreamWriter` with `AutoFlush = true` for reliable test-time readability. No Serilog flush timing issues. Echoes every entry to `Console.WriteLine` for live watching.

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ModelDebate.Manager.Debate.Interface;

namespace ModelDebate.Manager.Debate.Service
{
    public class DebateLogger : IDebateLogger, IDisposable
    {
        #region Members

        private readonly StreamWriter m_Writer;

        #region Props

        public string LogFilePath { get; }

        #endregion

        #endregion

        #region C'tor

        public DebateLogger(string logDirectory)
        {
            string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
            LogFilePath      = Path.Combine(logDirectory, $"debate-{timestamp}.log");
            m_Writer         = new StreamWriter(LogFilePath, append: false) { AutoFlush = true };
        }

        #endregion

        #region Public

        public Task LogAsync(IDebateMessage message, CancellationToken ct)
        {
            string formatted = Format(message);
            m_Writer.WriteLine(formatted);
            Console.WriteLine(formatted);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            m_Writer.Dispose();
        }

        #endregion

        #region Private

        private static string Format(IDebateMessage message)
        {
            if (message is DebateResponse response)
            {
                return FormatResponse(response);
            }

            if (message is ThinkingNotification thinking)
            {
                return $"[{thinking.SentAt:HH:mm:ss}] [{thinking.FromParticipant}] is thinking...";
            }

            if (message is HeartbeatPing heartbeat)
            {
                return $"[{heartbeat.SentAt:HH:mm:ss}] [Heartbeat from {heartbeat.FromParticipant}]";
            }

            if (message is SeedMessage seed)
            {
                return $"[{seed.SentAt:HH:mm:ss}] [Seed] Topic: {seed.Topic}";
            }

            return $"[{message.SentAt:HH:mm:ss}] [{message.Kind}] {message.FromParticipant}";
        }

        private static string FormatResponse(DebateResponse response)
        {
            return
                $"[{response.SentAt:HH:mm:ss}] " +
                $"[{response.FromParticipant}/{response.Metadata.ModelId}] " +
                $"[tokens: {response.Metadata.InputTokens} in / {response.Metadata.OutputTokens} out] " +
                $"[latency: {response.Metadata.Latency.TotalSeconds:F1}s]" +
                Environment.NewLine +
                response.Content +
                Environment.NewLine +
                "---";
        }

        #endregion
    }
}
```

**Step 2: Run the tests to verify they PASS**

```bash
dotnet test Test/Unit/Manager/Debate/Test.Unit.Manager.Debate.csproj \
    --filter "DebateLoggerSanity" -v normal
```

Expected: `Test Run Successful. Tests: 2 passed.`

> Note: the test calls `m_Logger.Dispose()` twice — once in the test body (before reading the file) and once in `TearDown`. `StreamWriter.Dispose()` is idempotent, so this is safe. If you see an `ObjectDisposedException`, remove the in-body `Dispose()` call and add a dedicated `Flush()` method instead.

**Step 3: Build and commit**

```bash
dotnet build ModelDebate.sln
git add -A && git commit -m "feat: add DebateLogger — AutoFlush StreamWriter, console echo, IDisposable"
```

---

## Task 3: RED — DebateManager: seed + first turn test

**Files to create:**
- `Test/Unit/Manager/Debate/Fakes.cs`
- `Test/Unit/Manager/Debate/DebateManagerSanity.cs`

> Define all fake implementations once in `Fakes.cs`. They are `internal sealed` classes — they live only in the test project.

**Step 1: Write `Fakes.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ModelDebate.Access.Chat.Interface;
using ModelDebate.Manager.Debate.Interface;

namespace Test.Unit.Manager.Debate
{
    // -----------------------------------------------------------------
    // FakeChatResource
    //   Records all received ChatRequests.
    //   Optionally delays, throws, or cancels a CancellationTokenSource
    //   after a specified number of calls.
    // -----------------------------------------------------------------
    internal sealed class FakeChatResource : IChatResource
    {
        public List<ChatRequest> ReceivedRequests { get; } = new List<ChatRequest>();

        private readonly ChatResponse             m_Response;
        private readonly TimeSpan                 m_Delay;
        private readonly int                      m_MaxCallsBeforeCancel;
        private readonly CancellationTokenSource? m_CtsToCancel;
        private readonly bool                     m_ShouldThrow;
        private          int                      m_CallCount;

        public FakeChatResource(
            ChatResponse             response,
            int                      maxCallsBeforeCancel = int.MaxValue,
            CancellationTokenSource? ctsToCancel          = null,
            TimeSpan                 delay                = default,
            bool                     shouldThrow          = false)
        {
            m_Response             = response;
            m_MaxCallsBeforeCancel = maxCallsBeforeCancel;
            m_CtsToCancel          = ctsToCancel;
            m_Delay                = delay;
            m_ShouldThrow          = shouldThrow;
        }

        public async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct)
        {
            ReceivedRequests.Add(request);

            if (m_ShouldThrow)
            {
                throw new System.Net.Http.HttpRequestException("Simulated API failure");
            }

            if (m_Delay > TimeSpan.Zero)
            {
                await Task.Delay(m_Delay, ct);
            }

            m_CallCount++;
            if (m_CallCount >= m_MaxCallsBeforeCancel)
            {
                m_CtsToCancel?.Cancel();
            }

            return m_Response;
        }
    }

    // -----------------------------------------------------------------
    // FakeDebateLogger — records all logged IDebateMessage instances
    // -----------------------------------------------------------------
    internal sealed class FakeDebateLogger : IDebateLogger
    {
        public List<IDebateMessage> LoggedMessages { get; } = new List<IDebateMessage>();

        public Task LogAsync(IDebateMessage message, CancellationToken ct)
        {
            LoggedMessages.Add(message);
            return Task.CompletedTask;
        }
    }

    // -----------------------------------------------------------------
    // Helper factory — builds a ChatResponse with reasonable defaults
    // -----------------------------------------------------------------
    internal static class FakeResponses
    {
        public static ChatResponse Make(
            string provider,
            string modelId,
            string content = "A witty debate argument.")
        {
            ModelMetadata metadata = new ModelMetadata(
                provider:     provider,
                modelId:      modelId,
                modelVersion: modelId,
                inputTokens:  42,
                outputTokens: 21,
                latency:      TimeSpan.FromMilliseconds(100));

            return new ChatResponse(content, metadata);
        }
    }
}
```

**Step 2: Write the failing test**

`Test/Unit/Manager/Debate/DebateManagerSanity.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        private static readonly string k_TempDir = Path.GetTempPath();

        #endregion

        #region Public

        [Test]
        public async Task GivenTopic_WhenSeed_ThenGptReceivesFirstMessage()
        {
            // Arrange
            using CancellationTokenSource cts = new CancellationTokenSource();

            FakeChatResource gptResource    = new FakeChatResource(
                response:            FakeResponses.Make("OpenAI", "gpt-4o", "GPT opening argument."),
                maxCallsBeforeCancel: 1,
                ctsToCancel:         cts);
            FakeChatResource claudeResource = new FakeChatResource(
                response:            FakeResponses.Make("Anthropic", "claude-3-5-sonnet", "Claude response."),
                maxCallsBeforeCancel: 1,
                ctsToCancel:         cts);
            FakeDebateLogger logger  = new FakeDebateLogger();
            DebateOptions    options = new DebateOptions("claude-3-5-sonnet-20241022", "gpt-4o", 30, k_TempDir);

            DebateManager manager = new DebateManager(claudeResource, gptResource, logger, options);
            SeedMessage   seed    = new SeedMessage("Is pineapple on pizza acceptable?");

            // Act
            try
            {
                await manager.RunAsync(seed, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // expected — FakeChatResource cancels the token after 1 GPT call
            }

            // Assert — GPT received exactly 1 ChatRequest containing the seed topic
            Assert.That(gptResource.ReceivedRequests.Count,             Is.GreaterThanOrEqualTo(1));
            Assert.That(gptResource.ReceivedRequests[0].UserMessage,    Does.Contain(seed.Topic));
        }

        #endregion

        #region Private

        #endregion
    }
}
```

**Step 3: Run the test to verify it FAILS**

```bash
dotnet test Test/Unit/Manager/Debate/Test.Unit.Manager.Debate.csproj \
    --filter "GivenTopic_WhenSeed_ThenGptReceivesFirstMessage" -v normal
```

Expected: FAIL — `The type or namespace name 'DebateManager' could not be found`

---

## Task 4: GREEN — DebateManager: seed + first turn

**Files to create:**
- `Manager/Debate/Service/DebateManager.cs`

**Step 1: Write the minimal implementation that makes the seed test pass**

> **Critical boundary rule:** `m_ClaudeResource` and `m_GptResource` only ever receive `ChatRequest` and return `ChatResponse`. All mapping from/to Manager types happens in the private `Map*` methods. `ChatResponse` is never passed to `IDebateLogger` or any Manager-facing code.

```csharp
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ModelDebate.Access.Chat.Interface;
using ModelDebate.iFx.Utilities;
using ModelDebate.Manager.Debate.Interface;

namespace ModelDebate.Manager.Debate.Service
{
    public class DebateManager : IDebateManager
    {
        #region Members

        private readonly IChatResource m_ClaudeResource;
        private readonly IChatResource m_GptResource;
        private readonly IDebateLogger m_Logger;
        private readonly DebateOptions m_Options;

        private const string k_ClaudeName = "Claude";
        private const string k_GptName    = "GPT";

        #region Props

        #endregion

        #endregion

        #region C'tor

        public DebateManager(
            IChatResource claudeResource,
            IChatResource gptResource,
            IDebateLogger logger,
            DebateOptions options)
        {
            Debug.Assert(claudeResource is not null, "claudeResource is not null");
            Debug.Assert(gptResource   is not null, "gptResource is not null");
            Debug.Assert(logger        is not null, "logger is not null");
            Debug.Assert(options       is not null, "options is not null");

            m_ClaudeResource = claudeResource;
            m_GptResource    = gptResource;
            m_Logger         = logger;
            m_Options        = options;
        }

        #endregion

        #region Public

        public async Task RunAsync(SeedMessage seed, CancellationToken ct)
        {
            Debug.Assert(seed is not null, "seed is not null");

            string systemPrompt = BuildSystemPrompt(seed.Topic);
            string lastMessage  = seed.Topic;   // GPT's first user message is the topic itself
            bool   claudeTurn   = false;         // GPT speaks first

            while (!ct.IsCancellationRequested)
            {
                IChatResource currentResource    = claudeTurn ? m_ClaudeResource : m_GptResource;
                string        currentSpeakerName = claudeTurn ? k_ClaudeName     : k_GptName;

                // Signal "thinking" before the call
                ThinkingNotification thinking = new ThinkingNotification(currentSpeakerName);
                await m_Logger.LogAsync(thinking, ct);

                // Build the Access-layer request — boundary map #1
                ChatRequest chatRequest = MapToRequest(systemPrompt, lastMessage);

                // Start per-turn timeout
                using CancellationTokenSource turnCts =
                    CancellationTokenSource.CreateLinkedTokenSource(ct);
                turnCts.CancelAfter(TimeSpan.FromSeconds(m_Options.TurnTimeoutSeconds));

                ChatResponse chatResponse;
                try
                {
                    chatResponse = await currentResource.CompleteAsync(chatRequest, turnCts.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // Turn timeout (outer ct is NOT cancelled — this is purely a turn timeout)
                    await HandleTurnTimeoutAsync(currentSpeakerName, ct);
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // API failure (HttpRequestException, network error, etc.)
                    await HandleApiFailureAsync(currentSpeakerName, ex, ct);
                    return;
                }

                // Map Access response → Manager response — boundary map #2
                DebateResponse debateResponse = MapToDebateResponse(currentSpeakerName, chatResponse);

                // Log via Manager contract — only Manager types cross this boundary
                await m_Logger.LogAsync(debateResponse, ct);

                // Prepare next turn
                lastMessage = chatResponse.Content;
                claudeTurn  = !claudeTurn;
            }
        }

        #endregion

        #region Private

        private static string BuildSystemPrompt(string topic)
        {
            return
                $"You are in a debate about the following topic: \"{topic}\". " +
                "Keep your responses to 2-3 paragraphs. " +
                "Your opponent is an AI. " +
                "Argue your position directly and concisely.";
        }

        // Boundary map #1 — Manager context → Access ChatRequest
        private static ChatRequest MapToRequest(string systemPrompt, string userMessage)
        {
            return new ChatRequest(systemPrompt, userMessage);
        }

        // Boundary map #2 — Access ChatResponse → Manager DebateResponse
        // chatResponse is consumed here and NEVER passed outside this method.
        private static DebateResponse MapToDebateResponse(
            string       participantName,
            ChatResponse chatResponse)
        {
            ResponseMetadata metadata = new ResponseMetadata(
                provider:     chatResponse.Metadata.Provider,
                modelId:      chatResponse.Metadata.ModelId,
                modelVersion: chatResponse.Metadata.ModelVersion,
                inputTokens:  chatResponse.Metadata.InputTokens,
                outputTokens: chatResponse.Metadata.OutputTokens,
                latency:      chatResponse.Metadata.Latency);

            return new DebateResponse(participantName, chatResponse.Content, metadata);
        }

        private async Task HandleTurnTimeoutAsync(string participantName, CancellationToken ct)
        {
            string notice = $"{participantName}:Timeout";
            ThinkingNotification timeoutMsg = new ThinkingNotification(notice);
            await m_Logger.LogAsync(timeoutMsg, ct);
        }

        private async Task HandleApiFailureAsync(
            string    participantName,
            Exception ex,
            CancellationToken ct)
        {
            string notice    = $"{participantName}:Error";
            ThinkingNotification errorMsg = new ThinkingNotification(notice);
            await m_Logger.LogAsync(errorMsg, ct);
        }

        #endregion
    }
}
```

**Step 2: Run the seed test to verify it PASSES**

```bash
dotnet test Test/Unit/Manager/Debate/Test.Unit.Manager.Debate.csproj \
    --filter "GivenTopic_WhenSeed_ThenGptReceivesFirstMessage" -v normal
```

Expected: `Test Run Successful. Tests: 1 passed.`

---

## Task 5: RED — DebateManager: turn alternation + boundary mapping tests

**Add 3 more test methods to `DebateManagerSanity.cs`:**

Open `Test/Unit/Manager/Debate/DebateManagerSanity.cs` and add the following test methods inside the `#region Public` block.

**Step 1: Add the new test methods**

```csharp
[Test]
public async Task GivenTwoTurns_WhenRun_ThenBothParticipantsRespond()
{
    // Arrange
    using CancellationTokenSource cts = new CancellationTokenSource();

    FakeChatResource gptResource = new FakeChatResource(
        response: FakeResponses.Make("OpenAI", "gpt-4o", "GPT says yes."));

    // Claude cancels after its first call — so we get exactly: GPT(1) → Claude(1) → stop
    FakeChatResource claudeResource = new FakeChatResource(
        response:            FakeResponses.Make("Anthropic", "claude-3-5-sonnet", "Claude says no."),
        maxCallsBeforeCancel: 1,
        ctsToCancel:         cts);

    FakeDebateLogger logger  = new FakeDebateLogger();
    DebateOptions    options = new DebateOptions("claude-3-5-sonnet-20241022", "gpt-4o", 30, k_TempDir);

    DebateManager manager = new DebateManager(claudeResource, gptResource, logger, options);
    SeedMessage   seed    = new SeedMessage("Remote work vs office?");

    // Act
    try
    {
        await manager.RunAsync(seed, cts.Token);
    }
    catch (OperationCanceledException) { /* expected */ }

    // Assert
    Assert.That(gptResource.ReceivedRequests.Count,   Is.EqualTo(1),   "GPT called exactly once");
    Assert.That(claudeResource.ReceivedRequests.Count, Is.EqualTo(1),   "Claude called exactly once");

    System.Collections.Generic.IEnumerable<DebateResponse> responses =
        logger.LoggedMessages.OfType<DebateResponse>();

    Assert.That(responses.Count(), Is.EqualTo(2), "2 DebateResponses logged (one per turn)");
}

[Test]
public async Task GivenTwoTurns_WhenRun_ThenClaudeReceivesGptResponseAsNextMessage()
{
    // Arrange — verify that the content from GPT's response becomes Claude's user message
    using CancellationTokenSource cts = new CancellationTokenSource();

    string gptContent = "GPT's unique argument content.";

    FakeChatResource gptResource = new FakeChatResource(
        response: FakeResponses.Make("OpenAI", "gpt-4o", gptContent));

    FakeChatResource claudeResource = new FakeChatResource(
        response:            FakeResponses.Make("Anthropic", "claude-3-5-sonnet", "Claude rebuttal."),
        maxCallsBeforeCancel: 1,
        ctsToCancel:         cts);

    FakeDebateLogger logger  = new FakeDebateLogger();
    DebateOptions    options = new DebateOptions("claude-3-5-sonnet-20241022", "gpt-4o", 30, k_TempDir);
    DebateManager    manager = new DebateManager(claudeResource, gptResource, logger, options);

    // Act
    try
    {
        await manager.RunAsync(new SeedMessage("Tabs vs spaces?"), cts.Token);
    }
    catch (OperationCanceledException) { /* expected */ }

    // Assert — Claude's first request UserMessage is the GPT response content
    Assert.That(claudeResource.ReceivedRequests.Count,              Is.GreaterThanOrEqualTo(1));
    Assert.That(claudeResource.ReceivedRequests[0].UserMessage,     Is.EqualTo(gptContent));
}

[Test]
public async Task GivenChatResponse_WhenMapped_ThenDebateResponseContainsCorrectMetadata()
{
    // Arrange — verify boundary mapping: Access ModelMetadata → Manager ResponseMetadata
    using CancellationTokenSource cts = new CancellationTokenSource();

    ModelMetadata   accessMeta = new ModelMetadata(
        provider:     "OpenAI",
        modelId:      "gpt-4o-2024-11-20",
        modelVersion: "gpt-4o-2024-11-20",
        inputTokens:  100,
        outputTokens: 50,
        latency:      TimeSpan.FromSeconds(2.0));

    ChatResponse    chatResponse  = new ChatResponse("Mapped content.", accessMeta);
    FakeChatResource gptResource  = new FakeChatResource(chatResponse,
        maxCallsBeforeCancel: 1, ctsToCancel: cts);
    FakeChatResource claudeResource = new FakeChatResource(
        FakeResponses.Make("Anthropic", "claude"));
    FakeDebateLogger logger  = new FakeDebateLogger();
    DebateOptions    options = new DebateOptions("claude", "gpt-4o", 30, k_TempDir);
    DebateManager    manager = new DebateManager(claudeResource, gptResource, logger, options);

    // Act
    try
    {
        await manager.RunAsync(new SeedMessage("Does mapping work?"), cts.Token);
    }
    catch (OperationCanceledException) { /* expected */ }

    // Assert — the DebateResponse in the log has metadata mapped from ChatResponse
    DebateResponse? debateResp = logger.LoggedMessages.OfType<DebateResponse>().FirstOrDefault();

    Assert.That(debateResp,                         Is.Not.Null,                  "DebateResponse was logged");
    Assert.That(debateResp!.Metadata.Provider,      Is.EqualTo("OpenAI"),         "provider mapped");
    Assert.That(debateResp.Metadata.ModelId,        Is.EqualTo("gpt-4o-2024-11-20"), "modelId mapped");
    Assert.That(debateResp.Metadata.InputTokens,    Is.EqualTo(100),              "inputTokens mapped");
    Assert.That(debateResp.Metadata.OutputTokens,   Is.EqualTo(50),               "outputTokens mapped");
    Assert.That(debateResp.Metadata.Latency,        Is.EqualTo(TimeSpan.FromSeconds(2.0)), "latency mapped");
    Assert.That(debateResp.Content,                 Is.EqualTo("Mapped content."), "content preserved");
}
```

**Step 2: Run the new tests to verify they FAIL**

```bash
dotnet test Test/Unit/Manager/Debate/Test.Unit.Manager.Debate.csproj \
    --filter "GivenTwoTurns" -v normal
dotnet test Test/Unit/Manager/Debate/Test.Unit.Manager.Debate.csproj \
    --filter "GivenChatResponse_WhenMapped" -v normal
```

Expected: FAIL — `DebateManager` exists but assertions fail (the implementation from Task 4 may already pass these — if so, that is fine: move directly to GREEN confirmation below).

---

## Task 6: GREEN — DebateManager: full turn loop + commit

> The `DebateManager` implementation from Task 4 already has the full turn loop and boundary mapping. Run all tests to confirm they pass.

**Step 1: Run the complete Manager test suite**

```bash
dotnet test Test/Unit/Manager/Debate/Test.Unit.Manager.Debate.csproj -v normal
```

Expected: `Test Run Successful. Tests: 5 passed` (2 from DebateLoggerSanity + 3 alternation tests + 1 seed test).

If any test fails, review the `DebateManager.RunAsync` logic:
- GPT is called FIRST (seed topic → GPT)
- `claudeTurn = !claudeTurn` swaps after every turn
- `lastMessage = chatResponse.Content` is what gets passed to the next participant
- `MapToDebateResponse` copies all fields from `chatResponse.Metadata` into a new `ResponseMetadata`

**Step 2: Build the full solution**

```bash
dotnet build ModelDebate.sln
```

Expected: `Build succeeded. 0 Error(s).`

**Step 3: Commit**

```bash
git add -A && git commit -m "feat: add DebateManager — full turn loop, alternation, boundary mapping"
```

---

## Task 7: RED — DebateManager: timeout test

**Add 1 test method to `DebateManagerSanity.cs`** in the `#region Public` block:

**Step 1: Write the failing test**

```csharp
[Test]
public async Task GivenSlowParticipant_WhenTurnExceedsTimeout_ThenDebateEndsCleanly()
{
    // Arrange — GPT takes 10 seconds; TurnTimeoutSeconds = 1
    using CancellationTokenSource cts = new CancellationTokenSource();

    FakeChatResource gptResource = new FakeChatResource(
        response: FakeResponses.Make("OpenAI", "gpt-4o"),
        delay:    TimeSpan.FromSeconds(10));  // much longer than timeout

    FakeChatResource claudeResource = new FakeChatResource(
        FakeResponses.Make("Anthropic", "claude-3-5-sonnet"));

    FakeDebateLogger logger  = new FakeDebateLogger();
    DebateOptions    options = new DebateOptions(
        anthropicModel:     "claude-3-5-sonnet-20241022",
        openAiModel:        "gpt-4o",
        turnTimeoutSeconds: 1,               // 1-second turn timeout
        logDirectory:       k_TempDir);

    DebateManager manager = new DebateManager(claudeResource, gptResource, logger, options);
    SeedMessage   seed    = new SeedMessage("Is async the future?");

    // Act — must complete well within 3 seconds despite 10s fake delay
    System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
    await manager.RunAsync(seed, cts.Token);
    sw.Stop();

    // Assert — RunAsync returned without cancellation from outer ct
    Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(3)), "debate ended quickly after timeout");

    // A ThinkingNotification with ":Timeout" in FromParticipant was logged
    bool timeoutLogged = false;
    foreach (IDebateMessage msg in logger.LoggedMessages)
    {
        if (msg is ThinkingNotification t && t.FromParticipant.Contains("Timeout"))
        {
            timeoutLogged = true;
            break;
        }
    }
    Assert.That(timeoutLogged, Is.True, "timeout notification was logged");
}
```

**Step 2: Run the test to verify it FAILS**

```bash
dotnet test Test/Unit/Manager/Debate/Test.Unit.Manager.Debate.csproj \
    --filter "GivenSlowParticipant" -v normal
```

Expected: the test either hangs for 10+ seconds OR fails because the current implementation doesn't have the turn timeout wired yet. (Actually, `RunAsync` from Task 4 already has the turn timeout logic. If the test passes, skip to Task 8.)

---

## Task 8: GREEN — DebateManager: timeout + commit

> The `DebateManager` from Task 4 already contains the timeout logic. If Task 7's test is already green, this task confirms it.

**Step 1: Run all Manager tests**

```bash
dotnet test Test/Unit/Manager/Debate/Test.Unit.Manager.Debate.csproj -v normal
```

Expected: all 6 tests pass (5 from previous tasks + 1 timeout test).

If the timeout test FAILS, check these areas in `DebateManager.RunAsync`:
1. `turnCts.CancelAfter(TimeSpan.FromSeconds(m_Options.TurnTimeoutSeconds))` is called before `CompleteAsync`
2. The `catch (OperationCanceledException) when (!ct.IsCancellationRequested)` guard correctly catches the turn-level cancellation
3. `HandleTurnTimeoutAsync` logs a `ThinkingNotification` whose `FromParticipant` contains `"Timeout"`
4. `return` is called after the timeout handler — the loop must exit

**Step 2: Commit**

```bash
git add -A && git commit -m "test: verify DebateManager timeout handling — turn CTS, Timeout notification"
```

---

## Task 9: RED — DebateManager: API error test

**Add 1 test method to `DebateManagerSanity.cs`:**

**Step 1: Write the failing test**

```csharp
[Test]
public async Task GivenApiFailed_WhenCompleteAsync_ThenDebateEndsAndErrorLogged()
{
    // Arrange — GPT throws an HttpRequestException
    using CancellationTokenSource cts = new CancellationTokenSource();

    FakeChatResource gptResource = new FakeChatResource(
        response:    FakeResponses.Make("OpenAI", "gpt-4o"),
        shouldThrow: true);  // throws HttpRequestException

    FakeChatResource claudeResource = new FakeChatResource(
        FakeResponses.Make("Anthropic", "claude-3-5-sonnet"));

    FakeDebateLogger logger  = new FakeDebateLogger();
    DebateOptions    options = new DebateOptions("claude-3-5-sonnet-20241022", "gpt-4o", 30, k_TempDir);

    DebateManager manager = new DebateManager(claudeResource, gptResource, logger, options);
    SeedMessage   seed    = new SeedMessage("Does error handling work?");

    // Act — RunAsync should complete (not throw) after API failure
    await manager.RunAsync(seed, cts.Token);

    // Assert — RunAsync returned gracefully (no exception propagated)
    // An Error ThinkingNotification was logged
    bool errorLogged = false;
    foreach (IDebateMessage msg in logger.LoggedMessages)
    {
        if (msg is ThinkingNotification t && t.FromParticipant.Contains("Error"))
        {
            errorLogged = true;
            break;
        }
    }
    Assert.That(errorLogged, Is.True, "error notification was logged");
}
```

**Step 2: Run the test to verify it FAILS**

```bash
dotnet test Test/Unit/Manager/Debate/Test.Unit.Manager.Debate.csproj \
    --filter "GivenApiFailed" -v normal
```

Expected: if the test is already green (Task 4 implementation handles this), move to Task 10 immediately.

---

## Task 10: GREEN — DebateManager: API error handling + commit

> The `DebateManager` from Task 4 already has the API error handler. Confirm all 7 tests pass.

**Step 1: Run all Manager tests**

```bash
dotnet test Test/Unit/Manager/Debate/Test.Unit.Manager.Debate.csproj -v normal
```

Expected: all 7 tests pass.

If the API error test FAILS, verify `DebateManager.RunAsync` has:
```csharp
catch (Exception ex) when (ex is not OperationCanceledException)
{
    await HandleApiFailureAsync(currentSpeakerName, ex, ct);
    return;
}
```

And `HandleApiFailureAsync` logs a `ThinkingNotification` whose `FromParticipant` contains `":Error"`.

**Step 2: Commit**

```bash
git add -A && git commit -m "test: verify DebateManager API failure path — error notification, clean return"
```

---

## Task 11: Program.cs + appsettings.json

**Files to create:**
- `Client/Runner/Program.cs`
- `Client/Runner/appsettings.json`

> `Program.cs` is the composition root — the ONLY place in the codebase that knows all concrete types simultaneously. It validates env vars, prompts the user, wires everything up, and starts the debate.

**Step 1: Write `Client/Runner/appsettings.json`**

```json
{
  "AnthropicModel":     "claude-3-5-sonnet-20241022",
  "OpenAiModel":        "gpt-4o",
  "TurnTimeoutSeconds": 60,
  "LogDirectory":       ""
}
```

Set the `appsettings.json` to copy to output:

Add to `Client/Runner/ModelDebate.Client.Runner.csproj` inside `<ItemGroup>`:

```xml
<ItemGroup>
    <None Update="appsettings.json">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
</ItemGroup>
```

**Step 2: Write `Client/Runner/Program.cs`**

> No DI framework. Direct construction at the composition root. `DebateLogger` is disposed at the end to flush the `StreamWriter`.

```csharp
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModelDebate.Access.Chat.Service.Claude;
using ModelDebate.Access.Chat.Service.OpenAI;
using ModelDebate.Manager.Debate.Interface;
using ModelDebate.Manager.Debate.Service;

namespace ModelDebate.Client.Runner
{
    internal static class Program
    {
        private static async Task Main()
        {
            // ── 1. Validate required environment variables ────────────────────────
            string? anthropicKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            string? openAiKey    = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

            if (string.IsNullOrEmpty(anthropicKey))
            {
                Console.Error.WriteLine("ERROR: ANTHROPIC_API_KEY is not set.");
                Environment.Exit(1);
            }

            if (string.IsNullOrEmpty(openAiKey))
            {
                Console.Error.WriteLine("ERROR: OPENAI_API_KEY is not set.");
                Environment.Exit(1);
            }

            // ── 2. Load configuration (env vars override appsettings.json) ────────
            string anthropicModel     = Environment.GetEnvironmentVariable("ANTHROPIC_MODEL")
                                        ?? "claude-3-5-sonnet-20241022";
            string openAiModel        = Environment.GetEnvironmentVariable("OPENAI_MODEL")
                                        ?? "gpt-4o";
            int    turnTimeoutSeconds = int.TryParse(
                                            Environment.GetEnvironmentVariable("TURN_TIMEOUT_SECONDS"),
                                            out int t) ? t : 60;
            string logDirectory       = Environment.GetEnvironmentVariable("LOG_DIR")
                                        ?? Directory.GetCurrentDirectory();

            // ── 3. Prompt user for the debate topic ────────────────────────────────
            Console.WriteLine("╔══════════════════════════════════════╗");
            Console.WriteLine("║         Claude vs GPT Debate         ║");
            Console.WriteLine("╚══════════════════════════════════════╝");
            Console.WriteLine();
            Console.Write("Enter debate topic: ");
            string? topic = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(topic))
            {
                Console.Error.WriteLine("ERROR: Topic cannot be empty.");
                Environment.Exit(1);
            }

            Console.WriteLine();
            Console.WriteLine($"Topic   : {topic}");
            Console.WriteLine($"Claude  : {anthropicModel}");
            Console.WriteLine($"GPT     : {openAiModel}");
            Console.WriteLine($"Timeout : {turnTimeoutSeconds}s per turn");
            Console.WriteLine($"Log dir : {logDirectory}");
            Console.WriteLine();
            Console.WriteLine("Press Ctrl+C to stop the debate at any time.");
            Console.WriteLine(new string('─', 50));
            Console.WriteLine();

            // ── 4. Wire Ctrl+C to a CancellationToken ─────────────────────────────
            using CancellationTokenSource cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;     // prevent the process from being killed immediately
                cts.Cancel();
            };

            // ── 5. Construct all components (composition root) ────────────────────
            ClaudeChatService  claudeService  = new ClaudeChatService(anthropicKey!, anthropicModel);
            OpenAiChatService  openAiService  = new OpenAiChatService(openAiKey!,    openAiModel);
            DebateOptions      options        = new DebateOptions(
                                                    anthropicModel,
                                                    openAiModel,
                                                    turnTimeoutSeconds,
                                                    logDirectory);

            using (DebateLogger debateLogger = new DebateLogger(logDirectory))
            {
                DebateManager manager = new DebateManager(
                    claudeResource: claudeService,
                    gptResource:    openAiService,
                    logger:         debateLogger,
                    options:        options);

                SeedMessage seed = new SeedMessage(topic!);

                // ── 6. Run the debate ──────────────────────────────────────────────
                try
                {
                    await manager.RunAsync(seed, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine();
                    Console.WriteLine("[Debate stopped by user]");
                }

                // ── 7. Print log file location ─────────────────────────────────────
                Console.WriteLine();
                Console.WriteLine(new string('─', 50));
                Console.WriteLine($"Debate transcript saved to: {debateLogger.LogFilePath}");
            }
        }
    }
}
```

**Step 3: Build the full solution**

```bash
dotnet build ModelDebate.sln
```

Expected: `Build succeeded. 0 Error(s).`

**Step 4: Commit**

```bash
git add -A && git commit -m "feat: add Program.cs — composition root, env var validation, Ctrl+C wiring"
```

---

## Task 12: End-to-end smoke run

**Step 1: Set environment variables**

```bash
export ANTHROPIC_API_KEY="your-anthropic-key"
export OPENAI_API_KEY="your-openai-key"
export TURN_TIMEOUT_SECONDS=30
export LOG_DIR=/tmp
```

**Step 2: Run the app**

```bash
cd /home/dkuida/code/model_debate
dotnet run --project Client/Runner/ModelDebate.Client.Runner.csproj
```

Expected:
```
╔══════════════════════════════════════╗
║         Claude vs GPT Debate         ║
╚══════════════════════════════════════╝

Enter debate topic: Is remote work better than office work?

Topic   : Is remote work better than office work?
Claude  : claude-3-5-sonnet-20241022
GPT     : gpt-4o
Timeout : 30s per turn
Log dir : /tmp

Press Ctrl+C to stop the debate at any time.
──────────────────────────────────────────────────

[HH:mm:ss] [GPT] is thinking...
[HH:mm:ss] [GPT/gpt-4o] [tokens: 312 in / 187 out] [latency: 2.3s]
Remote work has fundamentally transformed...
---
[HH:mm:ss] [Claude] is thinking...
[HH:mm:ss] [Claude/claude-3-5-sonnet-20241022] [tokens: 287 in / 201 out] [latency: 3.1s]
While remote work has advantages...
---
```

**Step 3: Stop after 2–3 turns with Ctrl+C**

Expected:
```
^C
[Debate stopped by user]

──────────────────────────────────────────────────
Debate transcript saved to: /tmp/debate-20260426-HHMMSS.log
```

**Step 4: Verify the log file**

```bash
cat /tmp/debate-20260426-HHMMSS.log
```

Verify:
- [ ] Each GPT turn shows `[GPT/gpt-4o]` with token counts and latency
- [ ] Each Claude turn shows `[Claude/claude-3-5-sonnet-20241022]` with token counts and latency
- [ ] Each response is followed by `---`
- [ ] "is thinking..." entries appear before each response
- [ ] The file is well-formed text (no garbled encoding)

**Step 5: Final commit**

```bash
git add -A && git commit -m "chore: verified end-to-end smoke run — debate runs, Ctrl+C clean shutdown, log correct"
```

---

## Phase 2 Completion Checklist

Before declaring the project complete, verify all of the following:

- [ ] `dotnet build ModelDebate.sln` succeeds with 0 errors
- [ ] `dotnet test Test/Unit/Manager/Debate/` passes all 7 tests
- [ ] `DebateManager` NEVER passes a `ChatResponse` to `IDebateLogger` or anything outside `MapToDebateResponse`
- [ ] `DebateManager` NEVER references types from `ModelDebate.Access.Chat.Service.Claude` or `ModelDebate.Access.Chat.Service.OpenAI` — only `IChatResource`
- [ ] `DebateLogger` implements `IDisposable` — `StreamWriter` is disposed on exit
- [ ] `Program.cs` references all concrete types (composition root), but the concrete types are never passed across boundaries — only interfaces are used in `DebateManager`'s constructor
- [ ] Smoke run produces well-formed log file with metadata on every turn
- [ ] Ctrl+C produces a clean shutdown with log file path printed to console

## Verify Layer Boundary Compliance

Run a quick grep to confirm no layer violations:

```bash
# Access services must NOT reference Manager types
grep -r "Manager.Debate" Access/Chat/Service.Claude/
grep -r "Manager.Debate" Access/Chat/Service.OpenAI/
# Both greps should return: no output (0 matches)

# Manager.Debate.Interface must NOT reference Access types
grep -r "Access.Chat" Manager/Debate/Interface/
# Should return: no output

# iFX must NOT reference Access or Manager types
grep -r "Access\|Manager" iFX/Utilities/
# Should return: no output
```

All four greps must return empty results. Any match is a boundary violation that must be fixed before shipping.
