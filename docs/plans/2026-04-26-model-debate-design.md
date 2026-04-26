# Model Debate Design

## Goal

A .NET console app where Claude (Anthropic) and GPT (OpenAI) autonomously debate a user-provided topic back and forth until the user stops it (Ctrl+C). Just for fun. No external infrastructure.

## Background

We want to watch two frontier LLMs argue with each other for entertainment. The app needs to be self-contained, run locally, capture the full transcript with model metadata, and shut down cleanly when the user has seen enough. No persistence beyond a log file, no UI beyond the console.

## Approach

Use `System.Threading.Channels` as the in-process message bus. Two `Channel<IDebateMessage>` instances (one per direction) carry messages between the two participants. A `DebateOrchestrator` owns the entire lifecycle — turn management, timers, routing, logging, shutdown. Participants are intentionally dumb: they receive a request, call their LLM, return a response.

**Why this approach:**
- `Channels` give us back-pressure-free, async, typed message passing with no third-party dependencies.
- Orchestrator-first design keeps the hard logic (timeouts, heartbeats, lifecycle) in one place that is easy to reason about and test.
- Participants reduce to a single method, making it trivial to swap models or add a third debater later.

## Architecture

```
[User provides topic]
        ↓
[DebateOrchestrator]
  ├─ Channel<IDebateMessage>  claudeInbox   ← GPT writes here, Claude reads
  ├─ Channel<IDebateMessage>  gptInbox      ← Claude writes here, GPT reads
  │
  ├─ Task: ClaudeParticipant
  │     reads claudeInbox → calls Anthropic SDK → writes gptInbox
  │
  └─ Task: GptParticipant
        reads gptInbox → calls OpenAI SDK → writes claudeInbox
```

The orchestrator seeds the debate by dropping the user's topic into `gptInbox` (GPT argues first, Claude responds). Both participant tasks run concurrently and loop indefinitely, blocking on their inbox channel until a message arrives. A shared `CancellationToken` (Ctrl+C) shuts everything down cleanly.

Each model receives a system prompt:

> "You are debating [topic]. Keep responses to 2-3 paragraphs. Your opponent is an AI. Argue your position directly."

### NuGet packages

- `Anthropic` — official .NET SDK
- `OpenAI` — official .NET SDK
- `Serilog` + `Serilog.Sinks.File` — file logging

### Project structure

```
model_debate/
  src/
    ModelDebate/
      Program.cs
      DebateOrchestrator.cs
      ClaudeParticipant.cs
      GptParticipant.cs
      DebateLogger.cs
      DebateMessage.cs
      ModelMetadata.cs
  ModelDebate.sln
```

API keys are read from environment variables: `ANTHROPIC_API_KEY`, `OPENAI_API_KEY`.

## Components

**Design principle: interface-first. Never pass primitives across boundaries. All types are classes/records so they can be extended.**

### Message type hierarchy

```
IDebateMessage (interface)
  ├── string MessageId
  ├── string FromParticipant
  ├── DateTimeOffset SentAt
  └── MessageKind Kind  (enum: Seed, Response, Thinking, Heartbeat)

DebateResponse : IDebateMessage
  ├── string Content
  └── ModelMetadata Metadata

ThinkingNotification : IDebateMessage   // "I'm processing, hold on"
HeartbeatPing        : IDebateMessage   // "Are you still alive?"
SeedMessage          : IDebateMessage   // initial topic from user
  └── string Topic
```

### ModelMetadata

Attached to every `DebateResponse` so the log records exactly which model produced what.

```
string   Provider       // "Anthropic" / "OpenAI"
string   ModelId        // "claude-3-5-sonnet-20241022" / "gpt-4o"
string   ModelVersion   // from response headers if available
int      InputTokens
int      OutputTokens
TimeSpan Latency
```

### Interfaces — all clients program to contracts

```
ILanguageModelClient   → CompleteAsync(DebateRequest)                       → Task<DebateResponse>
IDebateParticipant     → Name: string,
                         RespondAsync(DebateRequest, CancellationToken)     → Task<DebateResponse>
IDebateLogger          → LogAsync(IDebateMessage)                           → Task
IDebateOrchestrator    → RunAsync(SeedMessage, CancellationToken)           → Task
```

### Separation of concerns

| Responsibility | Owner |
|---|---|
| Heartbeat timers | Orchestrator |
| Timeout detection | Orchestrator |
| Message routing | Orchestrator |
| Turn management | Orchestrator |
| Debate lifecycle | Orchestrator |
| Logging | Orchestrator |
| Call LLM, return response | Participant (only this) |

### IDebateParticipant is intentionally dumb

```
RespondAsync(DebateRequest, CancellationToken) → Task<DebateResponse>
```

The participant receives a request, calls the LLM, returns a response. It knows nothing about channels, timers, routing, or orchestration. This keeps the surface area for adding new debaters tiny.

## Data Flow

### Startup sequence

1. App reads `ANTHROPIC_API_KEY` and `OPENAI_API_KEY` from environment. Missing → fail fast.
2. User types the debate topic on stdin.
3. Orchestrator creates two `Channel<IDebateMessage>` (unbounded).
4. Orchestrator writes a `SeedMessage` into `gptInbox` — GPT argues first.
5. Both participant tasks start. `CancellationToken` is wired to `Console.CancelKeyPress`.

### Orchestrator drives the entire loop

```
1. Seed: write SeedMessage → gptInbox (GPT argues first)
2. Loop:
   a. Write ThinkingNotification → waitingParticipant's inbox
      (waiting participant observes the other side is working)
   b. Start turn timer (configurable, default 60s)
   c. Call currentSpeaker.RespondAsync(request, ct)
   d. If timer expires before response → log timeout, cancel, exit
   e. Cancel timer on success
   f. Write DebateResponse → waitingParticipant's inbox
   g. Log response (with ModelMetadata)
   h. Swap speaker, repeat
```

### Heartbeat — orchestrator-owned

The orchestrator wraps each `RespondAsync` call with a `CancellationTokenSource` configured with the turn timeout. If the LLM call exceeds the threshold, the orchestrator declares the turn dead, logs it, and shuts down both channels cleanly. **No timer logic exists anywhere in participant code.**

### Log format

One block per message, written to file and echoed live to stdout:

```
[04:31:22] [GPT/gpt-4o] [tokens: 312 in / 187 out] [latency: 2.3s]
The free market has consistently shown...
---
```

Log file: `debate-{timestamp}.log` in `LOG_DIR`.

## Error Handling

**API failures (Anthropic / OpenAI call throws):**
- Caught at the orchestrator level (wrapping `RespondAsync`).
- Log the error with full context (model, tokens attempted, exception message).
- Cancel the shared `CancellationToken` — both channels drain, both tasks exit cleanly.
- No automatic retry — fail fast and exit.

**Turn timeout:**
- Orchestrator wraps `RespondAsync` with a timeout `CancellationTokenSource`.
- On expiry: log `"[Claude] No response after 60s. Ending debate."`, cancel, exit.

**Ctrl+C (CancellationToken):**
- All `channel.Reader.ReadAsync(ct)` calls propagate `OperationCanceledException`.
- Each participant wraps its loop in `try/catch OperationCanceledException` → clean exit.
- Logger flushes and closes file.
- App exits, prints debate log file path to console.

**Missing API keys at startup:**
- Validate both env vars before creating any clients.
- If missing: print clear message, `Environment.Exit(1)` — fail fast, nothing starts.

**Channel backpressure:**
- Channels are unbounded — no backpressure issue. The debate is strictly turn-based; never more than one unread message per channel at any time.

## Configuration

All configurable via environment variables or a simple `appsettings.json`:

| Setting | Default |
|---|---|
| `ANTHROPIC_API_KEY` | (required) |
| `OPENAI_API_KEY` | (required) |
| `ANTHROPIC_MODEL` | `claude-3-5-sonnet-20241022` |
| `OPENAI_MODEL` | `gpt-4o` |
| `TURN_TIMEOUT_SECONDS` | `60` |
| `LOG_DIR` | current directory |

## Testing Strategy

- **Unit tests** target the orchestrator with fake `IDebateParticipant` and `IDebateLogger` implementations. Cover: seed delivery, turn alternation, timeout firing, cancellation propagation, error path shutdown.
- **Unit tests** for each `IDebateParticipant` against a fake `ILanguageModelClient` to verify request shaping and metadata population.
- **Smoke test**: a single end-to-end run with real API keys, short turn timeout, manual Ctrl+C, verifying the log file is well-formed.
- No integration test harness for the real APIs — this is a fun app, not a production service.

## Open Questions

None. Scope is clear and complete.
