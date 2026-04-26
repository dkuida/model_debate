# Model Debate — Phase 1: Foundation

> **For execution:** Use `/execute-plan` mode or the subagent-driven-development recipe.

**Goal:** Solution scaffold + iFX infrastructure + all layer contracts + both Access Service implementations (Claude + OpenAI), fully TDD'd.

**Architecture:** IDesign/iFX layered architecture. iFX carries pure infrastructure (channel pair, error types). Access layer owns the `IChatResource` contract and its two implementations — one per LLM provider. No Manager types are referenced anywhere in the Access layer. The mapping between Access types and Manager types is explicitly deferred to Phase 2.

**Tech Stack:** .NET 10, C# 12, System.Threading.Channels, Anthropic SDK 12.17.0, OpenAI SDK 2.10.0, NUnit 4, Serilog 4.3.0

---

## Dependency Chain (NEVER violate this)

```
ModelDebate.iFx.Utilities            ← no project references
ModelDebate.Access.Chat.Interface    ← no project references (pure BCL + own types)
ModelDebate.Access.Chat.Service.Claude  → Access.Chat.Interface + [Anthropic NuGet]
ModelDebate.Access.Chat.Service.OpenAI  → Access.Chat.Interface + [OpenAI NuGet]
ModelDebate.Manager.Debate.Interface ← no project references (pure BCL + own types)
ModelDebate.Manager.Debate.Service   → Manager.Debate.Interface + Access.Chat.Interface + iFx.Utilities
ModelDebate.Client.Runner            → Service.Claude + Service.OpenAI + Manager.Debate.Service
Test.Unit.Access.Chat                → Service.Claude + Service.OpenAI
Test.Unit.Manager.Debate             → Manager.Debate.Service
```

## IDesign Coding Conventions (apply everywhere)

- Private fields: `m_` prefix + PascalCase (`m_Logger`, `m_Client`)
- Constants: `k_` prefix + PascalCase (`k_ClaudeName`)
- **No `var`** — every type declared explicitly
- **No implicit usings** — every `using` statement written explicitly
- One type per file
- `#region` order: `Members → (Props inside Members) → C'tor → Public → Private`
- `Debug.Assert` for every non-obvious assumption at method entry
- Classes not records (follow reference project convention)

---

## Task 1: Directory structure + global config files

**Files to create:**
- `global.json`
- `Directory.Build.props`
- `Directory.Packages.props`
- `Test/Unit/Directory.Packages.props`
- `Test/Integ/Directory.Packages.props`

**Step 1: Create the directory tree**

Run from `/home/dkuida/code/model_debate`:

```bash
mkdir -p iFX/Utilities
mkdir -p Access/Chat/Interface
mkdir -p Access/Chat/Service.Claude
mkdir -p Access/Chat/Service.OpenAI
mkdir -p Manager/Debate/Interface
mkdir -p Manager/Debate/Service
mkdir -p Client/Runner
mkdir -p Test/Unit/Access/Chat
mkdir -p Test/Unit/Manager/Debate
mkdir -p Test/Integ
```

**Step 2: Write `global.json`**

```json
{
    "sdk": {
        "version": "10.0.0",
        "rollForward": "latestFeature"
    }
}
```

**Step 3: Write `Directory.Build.props`**

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project>
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>disable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <WarningLevel>6</WarningLevel>
        <LangVersion>12.0</LangVersion>
        <AnalysisLevel>6.0</AnalysisLevel>
        <AnalysisMode>all</AnalysisMode>
        <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
        <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    </PropertyGroup>
    <PropertyGroup>
        <Authors>Model Debate</Authors>
        <Company>DDK Development</Company>
    </PropertyGroup>
</Project>
```

**Step 4: Write `Directory.Packages.props`**

> **Note on Anthropic SDK:** The official Anthropic SDK is `Anthropic` package by Anthropic (not the deprecated tryAGI version). Version 12.17.0 is the latest stable as of 2026-04-24. The OpenAI package is the official one from OpenAI.

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Anthropic"              Version="12.17.0" />
    <PackageVersion Include="OpenAI"                 Version="2.10.0"  />
    <PackageVersion Include="Serilog"                Version="4.3.0"   />
    <PackageVersion Include="Serilog.Sinks.File"     Version="7.0.0"   />
    <PackageVersion Include="Serilog.Sinks.Console"  Version="6.1.1"   />
  </ItemGroup>
</Project>
```

**Step 5: Write `Test/Unit/Directory.Packages.props`**

```xml
<Project>
    <Import Project="..\..\Directory.Packages.props"/>
    <ItemGroup>
        <PackageVersion Include="coverlet.collector"      Version="6.0.4"  />
        <PackageVersion Include="Microsoft.NET.Test.Sdk"  Version="17.14.0"/>
        <PackageVersion Include="NUnit"                   Version="4.3.2"  />
        <PackageVersion Include="NUnit.Analyzers"         Version="4.7.0"  />
        <PackageVersion Include="NUnit3TestAdapter"        Version="5.0.0"  />
    </ItemGroup>
</Project>
```

**Step 6: Write `Test/Integ/Directory.Packages.props`**

Same content as `Test/Unit/Directory.Packages.props` (placeholder for future integration tests):

```xml
<Project>
    <Import Project="..\..\Directory.Packages.props"/>
    <ItemGroup>
        <PackageVersion Include="coverlet.collector"      Version="6.0.4"  />
        <PackageVersion Include="Microsoft.NET.Test.Sdk"  Version="17.14.0"/>
        <PackageVersion Include="NUnit"                   Version="4.3.2"  />
        <PackageVersion Include="NUnit.Analyzers"         Version="4.7.0"  />
        <PackageVersion Include="NUnit3TestAdapter"        Version="5.0.0"  />
    </ItemGroup>
</Project>
```

---

## Task 2: All .csproj files + solution + build verification

**Files to create** (9 `.csproj` files + `ModelDebate.sln`):

**Step 1: Write all .csproj files**

`iFX/Utilities/ModelDebate.iFx.Utilities.csproj` — no project references, inherits everything from `Directory.Build.props`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <Configurations>Debug;Release</Configurations>
        <Platforms>x64</Platforms>
    </PropertyGroup>
</Project>
```

`Access/Chat/Interface/ModelDebate.Access.Chat.Interface.csproj` — no project references (only BCL types):

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <Configurations>Debug;Release</Configurations>
        <Platforms>x64</Platforms>
    </PropertyGroup>
</Project>
```

`Access/Chat/Service.Claude/ModelDebate.Access.Chat.Service.Claude.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <Configurations>Debug;Release</Configurations>
        <Platforms>x64</Platforms>
    </PropertyGroup>
    <ItemGroup>
        <ProjectReference Include="..\Interface\ModelDebate.Access.Chat.Interface.csproj" />
        <PackageReference Include="Anthropic" />
    </ItemGroup>
</Project>
```

`Access/Chat/Service.OpenAI/ModelDebate.Access.Chat.Service.OpenAI.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <Configurations>Debug;Release</Configurations>
        <Platforms>x64</Platforms>
    </PropertyGroup>
    <ItemGroup>
        <ProjectReference Include="..\Interface\ModelDebate.Access.Chat.Interface.csproj" />
        <PackageReference Include="OpenAI" />
    </ItemGroup>
</Project>
```

`Manager/Debate/Interface/ModelDebate.Manager.Debate.Interface.csproj` — no project references (only BCL types):

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <Configurations>Debug;Release</Configurations>
        <Platforms>x64</Platforms>
    </PropertyGroup>
</Project>
```

`Manager/Debate/Service/ModelDebate.Manager.Debate.Service.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <Configurations>Debug;Release</Configurations>
        <Platforms>x64</Platforms>
    </PropertyGroup>
    <ItemGroup>
        <ProjectReference Include="..\Interface\ModelDebate.Manager.Debate.Interface.csproj" />
        <ProjectReference Include="..\..\..\Access\Chat\Interface\ModelDebate.Access.Chat.Interface.csproj" />
        <ProjectReference Include="..\..\..\iFX\Utilities\ModelDebate.iFx.Utilities.csproj" />
        <PackageReference Include="Serilog"               />
        <PackageReference Include="Serilog.Sinks.File"    />
        <PackageReference Include="Serilog.Sinks.Console" />
    </ItemGroup>
</Project>
```

`Client/Runner/ModelDebate.Client.Runner.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <Configurations>Debug;Release</Configurations>
        <Platforms>x64</Platforms>
    </PropertyGroup>
    <ItemGroup>
        <ProjectReference Include="..\..\Access\Chat\Service.Claude\ModelDebate.Access.Chat.Service.Claude.csproj" />
        <ProjectReference Include="..\..\Access\Chat\Service.OpenAI\ModelDebate.Access.Chat.Service.OpenAI.csproj" />
        <ProjectReference Include="..\..\Manager\Debate\Service\ModelDebate.Manager.Debate.Service.csproj" />
    </ItemGroup>
</Project>
```

`Test/Unit/Access/Chat/Test.Unit.Access.Chat.csproj`:

> This test project calls **real APIs** using `ANTHROPIC_API_KEY` / `OPENAI_API_KEY` from the environment. These are integration-style unit tests. They will be skipped by CI unless keys are present.

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <Configurations>Debug;Release</Configurations>
        <Platforms>x64</Platforms>
        <IsPackable>false</IsPackable>
    </PropertyGroup>
    <ItemGroup>
        <ProjectReference Include="..\..\..\..\Access\Chat\Service.Claude\ModelDebate.Access.Chat.Service.Claude.csproj" />
        <ProjectReference Include="..\..\..\..\Access\Chat\Service.OpenAI\ModelDebate.Access.Chat.Service.OpenAI.csproj" />
        <PackageReference Include="Microsoft.NET.Test.Sdk" />
        <PackageReference Include="NUnit"                  />
        <PackageReference Include="NUnit.Analyzers"        />
        <PackageReference Include="NUnit3TestAdapter"      />
        <PackageReference Include="coverlet.collector"     />
    </ItemGroup>
</Project>
```

`Test/Unit/Manager/Debate/Test.Unit.Manager.Debate.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <Configurations>Debug;Release</Configurations>
        <Platforms>x64</Platforms>
        <IsPackable>false</IsPackable>
    </PropertyGroup>
    <ItemGroup>
        <ProjectReference Include="..\..\..\..\Manager\Debate\Service\ModelDebate.Manager.Debate.Service.csproj" />
        <PackageReference Include="Microsoft.NET.Test.Sdk" />
        <PackageReference Include="NUnit"                  />
        <PackageReference Include="NUnit.Analyzers"        />
        <PackageReference Include="NUnit3TestAdapter"      />
        <PackageReference Include="coverlet.collector"     />
    </ItemGroup>
</Project>
```

**Step 2: Create solution + add all projects**

```bash
cd /home/dkuida/code/model_debate
dotnet new sln -n ModelDebate
dotnet sln add iFX/Utilities/ModelDebate.iFx.Utilities.csproj
dotnet sln add Access/Chat/Interface/ModelDebate.Access.Chat.Interface.csproj
dotnet sln add Access/Chat/Service.Claude/ModelDebate.Access.Chat.Service.Claude.csproj
dotnet sln add Access/Chat/Service.OpenAI/ModelDebate.Access.Chat.Service.OpenAI.csproj
dotnet sln add Manager/Debate/Interface/ModelDebate.Manager.Debate.Interface.csproj
dotnet sln add Manager/Debate/Service/ModelDebate.Manager.Debate.Service.csproj
dotnet sln add Client/Runner/ModelDebate.Client.Runner.csproj
dotnet sln add Test/Unit/Access/Chat/Test.Unit.Access.Chat.csproj
dotnet sln add Test/Unit/Manager/Debate/Test.Unit.Manager.Debate.csproj
```

**Step 3: Verify the solution builds**

```bash
dotnet build ModelDebate.sln
```

Expected: `Build succeeded. 0 Error(s). 0 Warning(s)` (some warnings about empty projects are acceptable).

**Step 4: Commit**

```bash
git add -A && git commit -m "chore: scaffold solution — all projects, csproj files, global config"
```

---

## Task 3: iFX/Utilities types

**Files to create:**
- `iFX/Utilities/Error.cs`
- `iFX/Utilities/ErrorMessage.cs`
- `iFX/Utilities/DebateChannel.cs`

**Step 1: Write `iFX/Utilities/Error.cs`**

> iFX holds zero domain knowledge. `Error` is a reusable infrastructure type — the same class could live in any project unchanged.

```csharp
using System;

namespace ModelDebate.iFx.Utilities
{
    public class Error
    {
        #region Members

        #region Props

        public string Code        { get; }
        public string Description { get; }

        #endregion

        #endregion

        #region C'tor

        public Error(string code, string description)
        {
            Code        = code;
            Description = description;
        }

        public Error(Exception exception)
        {
            Code        = "Exception";
            Description = $"Message: {exception.Message}; StackTrace: {exception.StackTrace}";
        }

        #endregion

        #region Public

        public override string ToString() => $"[{Code}] {Description}";

        #endregion

        #region Private

        #endregion
    }
}
```

**Step 2: Write `iFX/Utilities/ErrorMessage.cs`**

```csharp
namespace ModelDebate.iFx.Utilities
{
    public static class ErrorMessage
    {
        public static readonly string ApiKeyMissing = "API key not configured.";
        public static readonly string TurnTimeout   = "Turn timed out with no response.";
        public static readonly string ApiFailed     = "LLM API call failed.";
    }
}
```

**Step 3: Write `iFX/Utilities/DebateChannel.cs`**

> Pure infrastructure. A typed bidirectional channel pair — two `Channel<T>`, one per direction. No domain knowledge here.

```csharp
using System.Threading.Channels;

namespace ModelDebate.iFx.Utilities
{
    public sealed class DebateChannel<T>
    {
        #region Members

        #region Props

        public Channel<T> ClaudeInbox { get; }
        public Channel<T> GptInbox    { get; }

        #endregion

        #endregion

        #region C'tor

        public DebateChannel()
        {
            ClaudeInbox = Channel.CreateUnbounded<T>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
            GptInbox = Channel.CreateUnbounded<T>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        }

        #endregion

        #region Public

        #endregion

        #region Private

        #endregion
    }
}
```

**Step 4: Build and verify**

```bash
dotnet build ModelDebate.sln
```

Expected: `Build succeeded. 0 Error(s).`

---

## Task 4: Access.Chat.Interface contracts

**Files to create (4 files, all in `Access/Chat/Interface/`):**
- `IChatResource.cs`
- `ChatRequest.cs`
- `ChatResponse.cs`
- `ModelMetadata.cs`

**Step 1: Write `IChatResource.cs`**

```csharp
using System.Threading;
using System.Threading.Tasks;

namespace ModelDebate.Access.Chat.Interface
{
    public interface IChatResource
    {
        Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct);
    }
}
```

**Step 2: Write `ChatRequest.cs`**

```csharp
namespace ModelDebate.Access.Chat.Interface
{
    public class ChatRequest
    {
        #region Members

        #region Props

        public string SystemPrompt { get; }
        public string UserMessage  { get; }

        #endregion

        #endregion

        #region C'tor

        public ChatRequest(string systemPrompt, string userMessage)
        {
            SystemPrompt = systemPrompt;
            UserMessage  = userMessage;
        }

        #endregion

        #region Public

        #endregion

        #region Private

        #endregion
    }
}
```

**Step 3: Write `ChatResponse.cs`**

```csharp
namespace ModelDebate.Access.Chat.Interface
{
    public class ChatResponse
    {
        #region Members

        #region Props

        public string        Content  { get; }
        public ModelMetadata Metadata { get; }

        #endregion

        #endregion

        #region C'tor

        public ChatResponse(string content, ModelMetadata metadata)
        {
            Content  = content;
            Metadata = metadata;
        }

        #endregion

        #region Public

        #endregion

        #region Private

        #endregion
    }
}
```

**Step 4: Write `ModelMetadata.cs`**

```csharp
using System;

namespace ModelDebate.Access.Chat.Interface
{
    public class ModelMetadata
    {
        #region Members

        #region Props

        public string   Provider     { get; }
        public string   ModelId      { get; }
        public string   ModelVersion { get; }
        public int      InputTokens  { get; }
        public int      OutputTokens { get; }
        public TimeSpan Latency      { get; }

        #endregion

        #endregion

        #region C'tor

        public ModelMetadata(
            string   provider,
            string   modelId,
            string   modelVersion,
            int      inputTokens,
            int      outputTokens,
            TimeSpan latency)
        {
            Provider     = provider;
            ModelId      = modelId;
            ModelVersion = modelVersion;
            InputTokens  = inputTokens;
            OutputTokens = outputTokens;
            Latency      = latency;
        }

        #endregion

        #region Public

        #endregion

        #region Private

        #endregion
    }
}
```

**Step 5: Build and verify**

```bash
dotnet build ModelDebate.sln
```

Expected: `Build succeeded. 0 Error(s).`

---

## Task 5: Manager.Debate.Interface contracts

**Files to create (10 files, all in `Manager/Debate/Interface/`):**

`MessageKind.cs`, `IDebateMessage.cs`, `SeedMessage.cs`, `ResponseMetadata.cs`, `DebateResponse.cs`, `ThinkingNotification.cs`, `HeartbeatPing.cs`, `IDebateManager.cs`, `IDebateLogger.cs`, `DebateOptions.cs`

> **Critical:** None of these files may reference any type from `Access.Chat.Interface`. The Manager layer defines its own types. `ResponseMetadata` is the Manager layer's own metadata — it mirrors the same fields as `Access.Chat.Interface.ModelMetadata` but is a separate class. The mapping between them happens in `DebateManager.Service` (Phase 2).

**Step 1: Write `MessageKind.cs`**

```csharp
namespace ModelDebate.Manager.Debate.Interface
{
    public enum MessageKind
    {
        Seed,
        Response,
        Thinking,
        Heartbeat
    }
}
```

**Step 2: Write `IDebateMessage.cs`**

```csharp
using System;

namespace ModelDebate.Manager.Debate.Interface
{
    public interface IDebateMessage
    {
        string         MessageId       { get; }
        string         FromParticipant { get; }
        DateTimeOffset SentAt          { get; }
        MessageKind    Kind            { get; }
    }
}
```

**Step 3: Write `SeedMessage.cs`**

```csharp
using System;

namespace ModelDebate.Manager.Debate.Interface
{
    public class SeedMessage : IDebateMessage
    {
        #region Members

        #region Props

        public string         MessageId       { get; }
        public string         FromParticipant { get; }
        public DateTimeOffset SentAt          { get; }
        public MessageKind    Kind            => MessageKind.Seed;
        public string         Topic           { get; }

        #endregion

        #endregion

        #region C'tor

        public SeedMessage(string topic, string fromParticipant = "User")
        {
            MessageId       = Guid.NewGuid().ToString("N");
            Topic           = topic;
            FromParticipant = fromParticipant;
            SentAt          = DateTimeOffset.UtcNow;
        }

        #endregion

        #region Public

        #endregion

        #region Private

        #endregion
    }
}
```

**Step 4: Write `ResponseMetadata.cs`**

> Manager layer's own metadata type. Same fields as `Access.Chat.Interface.ModelMetadata` but a separate class — the explicit mapping is what enforces the boundary. No cross-reference.

```csharp
using System;

namespace ModelDebate.Manager.Debate.Interface
{
    public class ResponseMetadata
    {
        #region Members

        #region Props

        public string   Provider     { get; }
        public string   ModelId      { get; }
        public string   ModelVersion { get; }
        public int      InputTokens  { get; }
        public int      OutputTokens { get; }
        public TimeSpan Latency      { get; }

        #endregion

        #endregion

        #region C'tor

        public ResponseMetadata(
            string   provider,
            string   modelId,
            string   modelVersion,
            int      inputTokens,
            int      outputTokens,
            TimeSpan latency)
        {
            Provider     = provider;
            ModelId      = modelId;
            ModelVersion = modelVersion;
            InputTokens  = inputTokens;
            OutputTokens = outputTokens;
            Latency      = latency;
        }

        #endregion

        #region Public

        #endregion

        #region Private

        #endregion
    }
}
```

**Step 5: Write `DebateResponse.cs`**

```csharp
using System;

namespace ModelDebate.Manager.Debate.Interface
{
    public class DebateResponse : IDebateMessage
    {
        #region Members

        #region Props

        public string           MessageId       { get; }
        public string           FromParticipant { get; }
        public DateTimeOffset   SentAt          { get; }
        public MessageKind      Kind            => MessageKind.Response;
        public string           Content         { get; }
        public ResponseMetadata Metadata        { get; }

        #endregion

        #endregion

        #region C'tor

        public DebateResponse(string fromParticipant, string content, ResponseMetadata metadata)
        {
            MessageId       = Guid.NewGuid().ToString("N");
            FromParticipant = fromParticipant;
            SentAt          = DateTimeOffset.UtcNow;
            Content         = content;
            Metadata        = metadata;
        }

        #endregion

        #region Public

        #endregion

        #region Private

        #endregion
    }
}
```

**Step 6: Write `ThinkingNotification.cs`**

```csharp
using System;

namespace ModelDebate.Manager.Debate.Interface
{
    public class ThinkingNotification : IDebateMessage
    {
        #region Members

        #region Props

        public string         MessageId       { get; }
        public string         FromParticipant { get; }
        public DateTimeOffset SentAt          { get; }
        public MessageKind    Kind            => MessageKind.Thinking;

        #endregion

        #endregion

        #region C'tor

        public ThinkingNotification(string fromParticipant)
        {
            MessageId       = Guid.NewGuid().ToString("N");
            FromParticipant = fromParticipant;
            SentAt          = DateTimeOffset.UtcNow;
        }

        #endregion

        #region Public

        #endregion

        #region Private

        #endregion
    }
}
```

**Step 7: Write `HeartbeatPing.cs`**

```csharp
using System;

namespace ModelDebate.Manager.Debate.Interface
{
    public class HeartbeatPing : IDebateMessage
    {
        #region Members

        #region Props

        public string         MessageId       { get; }
        public string         FromParticipant { get; }
        public DateTimeOffset SentAt          { get; }
        public MessageKind    Kind            => MessageKind.Heartbeat;

        #endregion

        #endregion

        #region C'tor

        public HeartbeatPing(string fromParticipant)
        {
            MessageId       = Guid.NewGuid().ToString("N");
            FromParticipant = fromParticipant;
            SentAt          = DateTimeOffset.UtcNow;
        }

        #endregion

        #region Public

        #endregion

        #region Private

        #endregion
    }
}
```

**Step 8: Write `IDebateManager.cs`**

```csharp
using System.Threading;
using System.Threading.Tasks;

namespace ModelDebate.Manager.Debate.Interface
{
    public interface IDebateManager
    {
        Task RunAsync(SeedMessage seed, CancellationToken ct);
    }
}
```

**Step 9: Write `IDebateLogger.cs`**

```csharp
using System.Threading;
using System.Threading.Tasks;

namespace ModelDebate.Manager.Debate.Interface
{
    public interface IDebateLogger
    {
        Task LogAsync(IDebateMessage message, CancellationToken ct);
    }
}
```

**Step 10: Write `DebateOptions.cs`**

```csharp
namespace ModelDebate.Manager.Debate.Interface
{
    public class DebateOptions
    {
        #region Members

        #region Props

        public string AnthropicModel     { get; }
        public string OpenAiModel        { get; }
        public int    TurnTimeoutSeconds { get; }
        public string LogDirectory       { get; }

        #endregion

        #endregion

        #region C'tor

        public DebateOptions(
            string anthropicModel,
            string openAiModel,
            int    turnTimeoutSeconds,
            string logDirectory)
        {
            AnthropicModel     = anthropicModel;
            OpenAiModel        = openAiModel;
            TurnTimeoutSeconds = turnTimeoutSeconds;
            LogDirectory       = logDirectory;
        }

        #endregion

        #region Public

        #endregion

        #region Private

        #endregion
    }
}
```

**Step 11: Build and verify**

```bash
dotnet build ModelDebate.sln
```

Expected: `Build succeeded. 0 Error(s).`

**Step 12: Commit**

```bash
git add -A && git commit -m "chore: all contracts established — iFX, Access.Chat.Interface, Manager.Debate.Interface"
```

---

## Task 6: RED — ClaudeChatService tests

**Files to create:**
- `Test/Unit/Access/Chat/ClaudeChatServiceSanity.cs`

> These tests call the **real Anthropic API**. Set `ANTHROPIC_API_KEY` in your environment before running. They will fail if the key is not set (that is the expected failure for the RED step — the class doesn't exist yet).

**Step 1: Write the failing test**

`Test/Unit/Access/Chat/ClaudeChatServiceSanity.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using ModelDebate.Access.Chat.Interface;
using ModelDebate.Access.Chat.Service.Claude;
using NUnit.Framework;

namespace Test.Unit.Access.Chat
{
    [TestFixture]
    public class ClaudeChatServiceSanity
    {
        #region Members

        private ClaudeChatService m_Service;

        #endregion

        #region C'tor / Setup

        [SetUp]
        public void SetUp()
        {
            string apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                ?? throw new InvalidOperationException(
                    "ANTHROPIC_API_KEY environment variable is not set. " +
                    "Export it before running these tests.");

            m_Service = new ClaudeChatService(apiKey, "claude-3-5-sonnet-20241022");
        }

        #endregion

        #region Public

        [Test]
        public async Task GivenValidRequest_WhenCompleteAsync_ThenResponseHasContent()
        {
            ChatRequest request = new ChatRequest(
                systemPrompt: "You are a helpful assistant. Reply in exactly one sentence.",
                userMessage:  "Say hello.");

            ChatResponse response = await m_Service.CompleteAsync(request, CancellationToken.None);

            Assert.That(response,         Is.Not.Null);
            Assert.That(response.Content, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task GivenValidRequest_WhenCompleteAsync_ThenMetadataPopulated()
        {
            ChatRequest request = new ChatRequest(
                systemPrompt: "You are a helpful assistant.",
                userMessage:  "What is 2 plus 2? Answer in one word.");

            ChatResponse response = await m_Service.CompleteAsync(request, CancellationToken.None);

            Assert.That(response.Metadata,              Is.Not.Null);
            Assert.That(response.Metadata.Provider,     Is.EqualTo("Anthropic"));
            Assert.That(response.Metadata.ModelId,      Is.Not.Null.And.Not.Empty);
            Assert.That(response.Metadata.InputTokens,  Is.GreaterThan(0));
            Assert.That(response.Metadata.OutputTokens, Is.GreaterThan(0));
            Assert.That(response.Metadata.Latency,      Is.GreaterThan(TimeSpan.Zero));
        }

        #endregion

        #region Private

        #endregion
    }
}
```

**Step 2: Run the tests to verify they FAIL**

```bash
dotnet test Test/Unit/Access/Chat/Test.Unit.Access.Chat.csproj -v normal
```

Expected: FAIL — `The type or namespace name 'ClaudeChatService' could not be found`

If it fails for a different reason (e.g. API key not set), that is also acceptable — the build should still fail with a compilation error proving the class doesn't exist.

---

## Task 7: GREEN — ClaudeChatService implementation

**Files to create:**
- `Access/Chat/Service.Claude/ClaudeChatService.cs`

**Step 1: Write the implementation**

> **SDK Note:** The `Anthropic` package v12+ is the official SDK by Anthropic. The constructor and API details below are based on the SDK README. If the constructor signature or property names differ, check the SDK docs at https://platform.claude.com/docs/en/api/sdks/csharp or examine the package in your IDE's object browser before running.

```csharp
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Models.Messages;
using ModelDebate.Access.Chat.Interface;

namespace ModelDebate.Access.Chat.Service.Claude
{
    public class ClaudeChatService : IChatResource
    {
        #region Members

        private readonly AnthropicClient m_Client;
        private readonly string          m_ModelId;

        #region Props

        #endregion

        #endregion

        #region C'tor

        // apiKey: value of ANTHROPIC_API_KEY
        // modelId: e.g. "claude-3-5-sonnet-20241022"
        public ClaudeChatService(string apiKey, string modelId)
        {
            Debug.Assert(!string.IsNullOrEmpty(apiKey),  "apiKey is not null or empty");
            Debug.Assert(!string.IsNullOrEmpty(modelId), "modelId is not null or empty");

            // If AnthropicClientOptions does not compile, check the SDK object browser
            // for the correct options class name and ApiKey property.
            m_Client  = new AnthropicClient(new AnthropicClientOptions { ApiKey = apiKey });
            m_ModelId = modelId;
        }

        #endregion

        #region Public

        public async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct)
        {
            Debug.Assert(request is not null, "request is not null");

            Stopwatch stopwatch = Stopwatch.StartNew();

            MessageCreateParams parameters = new()
            {
                Model    = m_ModelId,
                MaxTokens = 1024,
                System   = request.SystemPrompt,
                Messages =
                [
                    new()
                    {
                        Role    = Role.User,
                        Content = request.UserMessage,
                    }
                ],
            };

            Message message = await m_Client.Messages.Create(parameters, cancellationToken: ct);
            stopwatch.Stop();

            // If .Text does not compile, check whether Content[0] exposes .Text
            // or requires casting to a TextBlock type. Verify via SDK object browser.
            string content      = message.Content[0].Text;
            int    inputTokens  = message.Usage.InputTokens;
            int    outputTokens = message.Usage.OutputTokens;

            ModelMetadata metadata = new ModelMetadata(
                provider:     "Anthropic",
                modelId:      message.Model ?? m_ModelId,
                modelVersion: message.Model ?? m_ModelId,
                inputTokens:  inputTokens,
                outputTokens: outputTokens,
                latency:      stopwatch.Elapsed);

            return new ChatResponse(content, metadata);
        }

        #endregion

        #region Private

        #endregion
    }
}
```

**Step 2: Run the tests to verify they PASS**

```bash
dotnet test Test/Unit/Access/Chat/Test.Unit.Access.Chat.csproj \
    --filter "ClaudeChatServiceSanity" -v normal
```

Expected:
```
Test Run Successful.
Tests: 2 passed.
```

> If any assertion fails (wrong token count property name, wrong content access pattern), fix the implementation to match what the SDK actually returns. The tests are the spec — make them green.

**Step 3: Commit**

```bash
git add -A && git commit -m "feat: add ClaudeChatService implementing IChatResource"
```

---

## Task 8: RED — OpenAiChatService tests

**Files to create:**
- `Test/Unit/Access/Chat/OpenAiChatServiceSanity.cs`

> Requires `OPENAI_API_KEY` in the environment. Same pattern as ClaudeChatServiceSanity.

**Step 1: Write the failing test**

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using ModelDebate.Access.Chat.Interface;
using ModelDebate.Access.Chat.Service.OpenAI;
using NUnit.Framework;

namespace Test.Unit.Access.Chat
{
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
                    "Export it before running these tests.");

            m_Service = new OpenAiChatService(apiKey, "gpt-4o");
        }

        #endregion

        #region Public

        [Test]
        public async Task GivenValidRequest_WhenCompleteAsync_ThenResponseHasContent()
        {
            ChatRequest request = new ChatRequest(
                systemPrompt: "You are a helpful assistant. Reply in exactly one sentence.",
                userMessage:  "Say hello.");

            ChatResponse response = await m_Service.CompleteAsync(request, CancellationToken.None);

            Assert.That(response,         Is.Not.Null);
            Assert.That(response.Content, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task GivenValidRequest_WhenCompleteAsync_ThenMetadataPopulated()
        {
            ChatRequest request = new ChatRequest(
                systemPrompt: "You are a helpful assistant.",
                userMessage:  "What is 2 plus 2? Answer in one word.");

            ChatResponse response = await m_Service.CompleteAsync(request, CancellationToken.None);

            Assert.That(response.Metadata,              Is.Not.Null);
            Assert.That(response.Metadata.Provider,     Is.EqualTo("OpenAI"));
            Assert.That(response.Metadata.ModelId,      Is.Not.Null.And.Not.Empty);
            Assert.That(response.Metadata.InputTokens,  Is.GreaterThan(0));
            Assert.That(response.Metadata.OutputTokens, Is.GreaterThan(0));
            Assert.That(response.Metadata.Latency,      Is.GreaterThan(TimeSpan.Zero));
        }

        #endregion

        #region Private

        #endregion
    }
}
```

**Step 2: Run the tests to verify they FAIL**

```bash
dotnet test Test/Unit/Access/Chat/Test.Unit.Access.Chat.csproj \
    --filter "OpenAiChatServiceSanity" -v normal
```

Expected: FAIL — `The type or namespace name 'OpenAiChatService' could not be found`

---

## Task 9: GREEN — OpenAiChatService implementation

**Files to create:**
- `Access/Chat/Service.OpenAI/OpenAiChatService.cs`

**Step 1: Write the implementation**

> **SDK Note:** The `OpenAI` package v2.x is the official SDK from OpenAI. `ChatClient` is in `OpenAI.Chat` namespace. `CompleteChatAsync` returns `Task<ChatCompletion>` directly. Token counts are `completion.Usage.InputTokenCount` and `completion.Usage.OutputTokenCount`. Verify against SDK docs if property names differ: https://github.com/openai/openai-dotnet

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ModelDebate.Access.Chat.Interface;
using OpenAI.Chat;

namespace ModelDebate.Access.Chat.Service.OpenAI
{
    public class OpenAiChatService : IChatResource
    {
        #region Members

        private readonly ChatClient m_Client;
        private readonly string     m_ModelId;

        #region Props

        #endregion

        #endregion

        #region C'tor

        // apiKey: value of OPENAI_API_KEY
        // modelId: e.g. "gpt-4o"
        public OpenAiChatService(string apiKey, string modelId)
        {
            Debug.Assert(!string.IsNullOrEmpty(apiKey),  "apiKey is not null or empty");
            Debug.Assert(!string.IsNullOrEmpty(modelId), "modelId is not null or empty");

            m_Client  = new ChatClient(model: modelId, apiKey: apiKey);
            m_ModelId = modelId;
        }

        #endregion

        #region Public

        public async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct)
        {
            Debug.Assert(request is not null, "request is not null");

            Stopwatch stopwatch = Stopwatch.StartNew();

            List<ChatMessage> messages =
            [
                ChatMessage.CreateSystemMessage(request.SystemPrompt),
                ChatMessage.CreateUserMessage(request.UserMessage),
            ];

            ChatCompletion completion = await m_Client.CompleteChatAsync(
                messages,
                cancellationToken: ct);

            stopwatch.Stop();

            string content      = completion.Content[0].Text;
            int    inputTokens  = completion.Usage.InputTokenCount;
            int    outputTokens = completion.Usage.OutputTokenCount;
            string modelId      = completion.Model ?? m_ModelId;

            ModelMetadata metadata = new ModelMetadata(
                provider:     "OpenAI",
                modelId:      modelId,
                modelVersion: modelId,
                inputTokens:  inputTokens,
                outputTokens: outputTokens,
                latency:      stopwatch.Elapsed);

            return new ChatResponse(content, metadata);
        }

        #endregion

        #region Private

        #endregion
    }
}
```

**Step 2: Run all Access tests to verify they PASS**

```bash
dotnet test Test/Unit/Access/Chat/Test.Unit.Access.Chat.csproj -v normal
```

Expected:
```
Test Run Successful.
Tests: 4 passed.
```

> If `completion.Usage.InputTokenCount` doesn't exist, check the SDK's `ChatTokenUsage` type. The property may be `InputTokens`. Fix the implementation to match actual SDK property names.

**Step 3: Run full solution build to confirm no regressions**

```bash
dotnet build ModelDebate.sln
```

Expected: `Build succeeded. 0 Error(s).`

**Step 4: Commit**

```bash
git add -A && git commit -m "feat: add OpenAiChatService implementing IChatResource — Phase 1 complete"
```

---

## Phase 1 Completion Checklist

Before moving to Phase 2, verify all of the following:

- [ ] `dotnet build ModelDebate.sln` succeeds with 0 errors
- [ ] `dotnet test Test/Unit/Access/Chat/` passes all 4 tests (requires both API keys)
- [ ] `ClaudeChatService` only imports from `Anthropic.*` and `ModelDebate.Access.Chat.Interface` — no Manager types
- [ ] `OpenAiChatService` only imports from `OpenAI.*` and `ModelDebate.Access.Chat.Interface` — no Manager types
- [ ] iFX types (`Error`, `ErrorMessage`, `DebateChannel`) have zero domain references
- [ ] No type from `Manager.Debate.Interface` appears anywhere in `Access.*` projects
- [ ] All fields use `m_` prefix, no `var`, no implicit usings

Continue to: `docs/plans/2026-04-26-model-debate-phase2.md`
