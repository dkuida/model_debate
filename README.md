# Model Debate 🤖

**Claude vs GPT, arguing in your terminal until one of you presses Ctrl+C.**

A .NET 10 console app that pits Anthropic's Claude against OpenAI's GPT in an autonomous, turn-based debate on any topic you give it. Claude opens, GPT rebuts, Claude responds, and so on — with full transcripts saved to a timestamped log. Built on the IDesign / iFX layered architecture because if the models are going to argue forever, the code under them ought to be tidy.

---

## What it does

You type a topic. Claude opens. GPT responds. The two models alternate turns until you stop them or the configured turn limit is reached. Every turn is logged to disk with model name, token counts, and per-turn latency.

```
╔══════════════════════════════════════╗
║         Claude vs GPT Debate         ║
╚══════════════════════════════════════╝

Enter debate topic: Should pineapple go on pizza?

Topic     : Should pineapple go on pizza?
Claude    : claude-sonnet-4-6
GPT       : gpt-4.1-mini
Timeout   : 60s per turn
Max turns : 10
Log dir   : /tmp

Press Ctrl+C to stop the debate at any time.
──────────────────────────────────────────────────

[14:22:01] [Claude] is thinking...
[14:22:03] [Claude/claude-sonnet-4-6] [tokens: 287 in / 201 out] [latency: 2.1s]
Pineapple on pizza is not merely acceptable — it is a triumph of culinary
creativity. The interplay of sweet and savoury...
---
[14:22:03] [GPT] is thinking...
[14:22:05] [GPT/gpt-4.1-mini] [tokens: 312 in / 187 out] [latency: 2.3s]
With respect, my colleague conflates novelty with quality. Pizza is an Italian
institution built on the harmony of...
---
```

## Quick start

```bash
export ANTHROPIC_API_KEY="sk-ant-..."
export OPENAI_API_KEY="sk-..."
dotnet run --project Client/Runner/ModelDebate.Client.Runner.csproj
```

Enter a topic when prompted. Ctrl+C stops the debate cleanly and flushes the log.

## Configuration

Settings resolve in this order: **environment variables → `appsettings.json` → hardcoded defaults**.

| Setting           | Env var                | JSON key             | Default              |
|-------------------|------------------------|----------------------|----------------------|
| Anthropic API key | `ANTHROPIC_API_KEY`    | — (never in files)   | required             |
| OpenAI API key    | `OPENAI_API_KEY`       | —                    | required             |
| Claude model      | `ANTHROPIC_MODEL`      | `AnthropicModel`     | `claude-sonnet-4-6`  |
| GPT model         | `OPENAI_MODEL`         | `OpenAiModel`        | `gpt-4.1-mini`       |
| Turn timeout      | `TURN_TIMEOUT_SECONDS` | `TurnTimeoutSeconds` | `60`                 |
| Max turns         | `MAX_TURNS`            | `MaxTurns`           | `10`                 |
| Log directory     | `LOG_DIR`              | `LogDirectory`       | current dir          |

API keys are read from environment only — they never appear in `appsettings.json`.

## How it works

The app follows the **IDesign / iFX layered architecture** (Juval Löwy). Each layer is split into an `Interface/` project (contracts and DTOs) and a `Service/` project (implementation). No types leak across layer boundaries — explicit mapping methods translate between layers at every seam.

### Message flow

```
                ┌──────────────────────┐
                │  Client/Runner       │   topic, Ctrl+C
                │  (composition root)  │◀──────────────── user
                └──────────┬───────────┘
                           │ resolves via Autofac
                           ▼
              ┌──────────────────────────┐
              │     DebateManager        │   owns turn routing,
              │  (the orchestrator)      │   timeouts, max-turn
              └──────┬─────────────┬─────┘   enforcement, logging
                     │             │
       ChatRequest   │             │  ChatRequest
   (via Channel<T>)  ▼             ▼  (via Channel<T>)
              ┌──────────┐   ┌──────────┐
              │ Claude   │   │  GPT     │   participants are dumb:
              │ Service  │   │ Service  │   request in, response out
              └────┬─────┘   └────┬─────┘
                   │              │
                   ▼              ▼
              Anthropic SDK   OpenAI SDK
```

### Key design choices

- **`System.Threading.Channels` as the message bus.** Typed, async, in-process, zero infrastructure. No queues, no brokers, no Kafka, no apologies.
- **`DebateManager` owns all control logic.** Turn routing, per-turn timeouts, max-turn enforcement, transcript logging — all centralised. Participants (`Service.Claude`, `Service.OpenAI`) implement `IChatResource`: receive a `ChatRequest`, return a `ChatResponse`. They know nothing about the debate.
- **Explicit boundary mapping.** `MapToRequest()` and `MapToDebateResponse()` are the *only* places where Access-layer types (`ChatRequest`, `ChatResponse`) cross into Manager-layer types (`DebateMessage`, `DebateResponse`). No leaks, no shortcuts.
- **Autofac for DI.** `Program.cs` is the composition root. Modules per layer.
- **Serilog via `ILogger<T>` / `ILoggerFactory`.** Configured from `appsettings.json`. Structured properties on every event.
- **Plain `ConfigurationBuilder`.** No ASP.NET pulled in for a console app. JSON + env vars + defaults, that's it.

## Log output

Each turn is written as a self-contained block — same format you see on stdout, minus the spinner:

```
[14:22:03] [Claude/claude-sonnet-4-6] [tokens: 287 in / 201 out] [latency: 2.1s]
Pineapple on pizza is not merely acceptable...
---
```

File name: `debate-YYYYMMDD-HHmmss.log`, written to `LOG_DIR` (default: current directory).

## Project structure

```
ModelDebate.sln
│
├── iFX/
│   └── Utilities/                          # pure infra: error types, channel factory
│       ├── Interface/
│       └── Service/
│
├── Access/
│   └── Chat/
│       ├── Interface/                      # IChatResource, ChatRequest, ChatResponse,
│       │                                   # ModelMetadata
│       ├── Service.Claude/                 # wraps Anthropic SDK
│       └── Service.OpenAI/                 # wraps OpenAI SDK
│
├── Manager/
│   └── Debate/
│       ├── Interface/                      # IDebateManager, IDebateLogger,
│       │                                   # DebateMessage hierarchy
│       └── Service/                        # DebateManager (orchestrator),
│                                           # DebateLogger
│
├── Client/
│   └── Runner/                             # Program.cs — Autofac composition root,
│                                           # appsettings.json, console UI
│
└── Tests/                                  # NUnit test projects, one per Service
```

Every `Interface/` and `Service/` is its own `.csproj`. Service projects depend on their own Interface and on lower-layer Interfaces only — never on another Service directly.

## Tests

```bash
dotnet test
```

47 NUnit tests covering:

- `iFX/Utilities` — channel factory behaviour, error type semantics
- `Access/Chat/Service.Claude` — request mapping, response parsing, error translation
- `Access/Chat/Service.OpenAI` — same coverage as Claude service
- `Manager/Debate/Service` — turn routing, timeout handling, max-turn enforcement, cancellation, logger formatting

External SDK calls are stubbed at the `IChatResource` boundary, so the suite runs offline with no API keys.

## Requirements

- .NET 10 SDK
- An `ANTHROPIC_API_KEY`
- An `OPENAI_API_KEY`
- A topic the two models can argue about. (They can argue about anything.)
