# Model Debate

**A showcase of AI agentic development workflows — the app itself is a bonus.**

Two LLMs argue in your terminal until you press Ctrl+C. But that's not why this repo exists.

---

## What This Is Really About

This project was built end-to-end using **[Amplifier](https://github.com/microsoft/amplifier)** with the **[Superpowers](https://github.com/microsoft/amplifier-bundle-superpowers)** methodology: a structured, multi-agent workflow where AI does the work but humans stay in control at every decision point.

The repo is a live artifact of that process. Every commit, every design document, every test-first step is exactly what the workflow produced.

---

## The Development Workflow

### Stage 1: Brainstorm (`/brainstorm`)

Before a single line of code, the `/brainstorm` mode ran a collaborative design session. The output was a formal design document (`docs/plans/2026-04-26-model-debate-design.md`) that pinned down:

- The architecture (IDesign/iFX layered, `System.Threading.Channels` as message bus)
- All interfaces and contracts up front
- The exact boundary rules — what each layer may and may not reference
- Error handling strategy and configuration scheme

No code until the design was solid.

### Stage 2: Write Plan (`/write-plan`)

The design document fed into `/write-plan`, which decomposed the work into two phases of bite-sized, TDD-ready tasks:

- **Phase 1** (`docs/plans/2026-04-26-model-debate-phase1.md`): Foundation — solution scaffold, iFX infrastructure, all layer contracts, both LLM service implementations, tested against real APIs
- **Phase 2** (`docs/plans/2026-04-26-model-debate-phase2.md`): Integration — `DebateManager` orchestrator, logger, dependency injection wiring, console runner

Each task in the plan was written with explicit RED/GREEN/REFACTOR steps, exact file paths, and the expected commit message. No ambiguity for the executing agent.

### Stage 3: Execute Plan (`/execute-plan`)

The plan was handed to the subagent-driven development pipeline. A fresh agent was spawned per task. Each task followed the same discipline:

1. **RED** — write the failing test first, verify it fails for the right reason
2. **GREEN** — write the minimal implementation to pass
3. **REFACTOR** — clean up, stay green
4. **Commit** — conventional message, one logical unit

The git log is the receipt:

```
6c3f09d feat(task-6): add RED-step failing tests for ClaudeChatService
a0e3091 feat: add ClaudeChatService implementing IChatResource
0d8020e feat(task-8): add RED-step failing tests for OpenAiChatService
01db598 feat: add OpenAiChatService implementing IChatResource — Phase 1 complete
d0ed14e feat: add DebateManager + tests — full turn loop, boundary mapping, timeout, error handling
```

Every `RED-step` commit proves the test was written before the implementation. Every implementation commit proves it was written to make a specific failing test green.

---

## Artifacts in This Repo

| Artifact | What it shows |
|----------|---------------|
| `docs/plans/2026-04-26-model-debate-design.md` | Design doc produced by `/brainstorm` — full architecture, interfaces, data flow, error strategy |
| `docs/plans/2026-04-26-model-debate-phase1.md` | Phase 1 plan — 9 tasks, explicit TDD steps, exact file paths, build verification at each step |
| `docs/plans/2026-04-26-model-debate-phase2.md` | Phase 2 plan — DebateManager, logger, DI wiring, console runner |
| `git log` | The execution trail — RED commits before GREEN commits, one logical unit per commit |
| `Test/` | 47 NUnit tests — every service fully covered, external SDKs stubbed at the `IChatResource` boundary |

---

## The App (Secondary)

Claude and GPT debate any topic you give them, turn by turn, until you stop them. Transcripts are saved with model name, token counts, and per-turn latency.

```
╔══════════════════════════════════════╗
║       Claude vs GPT Debate          ║
╚══════════════════════════════════════╝

Enter debate topic: Should pineapple go on pizza?

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

### Quick start

```bash
export ANTHROPIC_API_KEY="sk-ant-..."
export OPENAI_API_KEY="sk-..."
dotnet run --project Client/Runner/ModelDebate.Client.Runner.csproj
```

### Configuration

Settings resolve: **environment variables → `appsettings.json` → hardcoded defaults**.

| Setting | Env var | Default |
|---------|---------|---------|
| Claude model | `ANTHROPIC_MODEL` | `claude-sonnet-4-6` |
| GPT model | `OPENAI_MODEL` | `gpt-4.1-mini` |
| Turn timeout | `TURN_TIMEOUT_SECONDS` | `60` |
| Max turns | `MAX_TURNS` | `30` |
| Log directory | `LOG_DIR` | current dir |

API keys are read from environment only — they never appear in `appsettings.json`.

### Architecture

IDesign/iFX layered architecture. Each layer is split into `Interface/` (contracts and DTOs) and `Service/` (implementation). No types leak across layer boundaries — explicit mapping methods translate between layers at every seam.

```
Client/Runner  →  DebateManager  →  IChatResource  →  Anthropic/OpenAI SDKs
                  (orchestrator)     (Claude, GPT)
```

### Tests

```bash
dotnet test
```

47 NUnit tests. External SDK calls are stubbed at the `IChatResource` boundary — the suite runs offline with no API keys.

### Requirements

- .NET 10 SDK
- `ANTHROPIC_API_KEY`
- `OPENAI_API_KEY`

---

## Tools Used

- **[Amplifier](https://github.com/microsoft/amplifier)** — AI agent framework
- **[Superpowers](https://github.com/microsoft/amplifier-bundle-superpowers)** — structured development methodology (`/brainstorm`, `/write-plan`, `/execute-plan`)
- **.NET 10**, C# 12, NUnit 4, Autofac, Serilog
