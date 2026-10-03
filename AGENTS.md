# Agent Development Guide

This file is for Codex and future coding agents working on AICharacterChat. It is a long-term working agreement, not a current progress log.

## Project Working Principles

- Read the relevant source and tests before changing behavior.
- Keep changes scoped to the requested phase or bug.
- Do not start future phases unless the user explicitly asks.
- Do not commit personal data, API keys, `bin/`, `obj/`, or temporary files.
- Prefer existing project patterns over new abstractions.

## Build/Test Policy

- Baseline commands:

```bash
dotnet build AICharacterChat.sln
dotnet test AICharacterChat.sln
```

- Run focused tests when developing a narrow change.
- Run the full build and test suite before committing code changes unless the user explicitly limits the task to documentation.
- Tests must not call the real Anthropic API. Use fake `IChatModelClient` implementations.
- Keep the project on `net8.0-windows` unless a later task explicitly performs a verified framework migration. Do not use `net9.0-windows` as an intermediate target.

## Layer Boundaries

The solution is one WPF application organized by responsibility:

```text
Domain
Application
Infrastructure
Presentation
```

- `Domain` contains data objects and domain concepts only.
- `Application` contains provider-independent chat flow, context building, summary services, interfaces, and request models.
- `Infrastructure` contains Anthropic HTTP integration and JSON persistence.
- `Presentation` contains WPF windows, presentation models, and view models.

Presentation ViewModels may depend on Application services and interfaces. Application code must not depend on WPF windows or Anthropic implementation classes.

## WPF/MVVM Boundaries

`MainWindow.xaml.cs` is the composition root for WPF dialogs and concrete infrastructure objects. Keep it limited to dependency construction, modal window opening, view lifecycle, Enter-key handling, and scrolling.

Do not move API calls, prompt construction, JSON serialization, persistence rules, or chat-state decisions into code-behind.

## No Direct Domain TwoWay Editing

Presentation edit screens should use editable buffers or presentation projections. Do not bind mutable domain objects directly to UI fields when unsaved edits, cancel, rollback, or confirmation behavior is required.

## Persistence Service Policy

Domain validation and mutation should stay in domain/application services. Persistence services coordinate approved mutations with `IWorldRepository.SaveAsync` and must preserve in-memory consistency on save failure or cancellation.

Do not introduce a new repository abstraction unless the storage boundary actually changes.

## Exact Object Reference Targeting Policy

Operations on existing summaries must target the exact selected `ConversationSummary` object reference. Do not select update/delete/regenerate targets by taking the first matching id, because external JSON can contain duplicate ids before load repair or inside test scenarios.

## Raw ChatMessage Preservation

`ChatMessage.Content` for user messages stores the raw user input. Provider-specific request wrapping, such as `[현재 상황 서술]`, happens only while building request messages.

Summary generation, regeneration, delete, and context rendering must not mutate raw source messages.

## Summary Range Immutability

Persisted `ConversationSummary.StartMessageId` and `ConversationSummary.EndMessageId` define an immutable inclusive range. Editing a saved summary changes title and section content only. To change the range, delete the old summary and create a new one.

## Summary Generation/Regeneration No-Auto-Save Policy

AI-generated summary output is draft data until the user explicitly saves. Initial summary generation and management regeneration must not mutate `ChatSession.Summaries` or save repositories by themselves.

## Recent/Historical Source-of-Truth Policy

`ChatSession.Messages` remains the full persisted conversation history. `RecentMessageSelector` creates a request-time window only. `HistoricalContextBuilder` renders older context from messages, summaries, and the actual recent selection; historical context is not persisted.

## Async Lifecycle / Late-Result Safety

Presentation workflows that call AI or persistence asynchronously must guard against stale continuations. Use cancellation, operation identity checks, and closed-state guards so late success/failure cannot update disposed windows, overwrite user edits, or mutate domain state after cancellation/close.

## Scope Discipline

Do not implement broad memory systems, embeddings, vector search, automatic summaries, or cross-session summary management unless explicitly requested. Keep phase work narrow and test the risky behavior at the time it is implemented.

## Commit Conventions

- Use a Conventional Commit prefix when committing, such as `feat:`, `fix:`, `refactor:`, `test:`, or `docs:`.
- Korean subject/body text is acceptable and preferred when the user requests it.
- Commit only after the requested verification succeeds.
- Do not rewrite or squash existing commits unless the user explicitly asks.

## Implementation Reporting Requirements

When reporting completed work, include:

- What changed.
- What was intentionally not changed.
- Build/test results or a clear note that they were not run.
- Any residual risk or manual verification needed.

## Document Maintenance Policy

### Update Every Phase / Verified Commit

Update [docs/CODEX_HANDOFF.md](docs/CODEX_HANDOFF.md) when any of the following occurs:

- A phase or important feature is completed.
- Audit and commit are completed.
- Build/test baseline changes.
- The next task changes.
- New deferred technical debt is accepted.
- An important architecture decision is finalized.

Minimum handoff updates:

- latest verified commit
- build/test baseline
- completed phase
- important new decisions
- next phase
- deferred backlog

### Update Only When the Owned Structure Changes

Do not edit these files on every task:

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- [docs/CONTEXT.md](docs/CONTEXT.md)
- [docs/DATA_MODEL.md](docs/DATA_MODEL.md)
- [docs/SUMMARIES.md](docs/SUMMARIES.md)

Update them only when their source-of-truth area changes:

- Layer/component relationships -> [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- Recent/Historical context semantics -> [docs/CONTEXT.md](docs/CONTEXT.md)
- Domain model, JSON schema, ownership, invariants -> [docs/DATA_MODEL.md](docs/DATA_MODEL.md)
- Summary behavior and rules -> [docs/SUMMARIES.md](docs/SUMMARIES.md)

If a bug fix does not change the documented behavior, do not churn docs.

### Stable Documents

Update [README.md](README.md) only when the project purpose, technology stack, build/run steps, required configuration, or documentation structure changes.

Update this `AGENTS.md` only when long-term workflow, architecture principles, agent rules, commit rules, document policy, or core invariants change.

Update [docs/MIGRATION.md](docs/MIGRATION.md) only when legacy migration behavior changes.

## Document Source of Truth

```text
Project introduction
-> README.md

Long-term agent/development rules
-> AGENTS.md

Current development state
-> docs/CODEX_HANDOFF.md

System architecture
-> docs/ARCHITECTURE.md

AI context semantics
-> docs/CONTEXT.md

Persisted/domain schema
-> docs/DATA_MODEL.md

Legacy migration
-> docs/MIGRATION.md

Summary subsystem
-> docs/SUMMARIES.md
```

Prefer linking to the owning document instead of repeating the same details in multiple places.

## Conflict Priority

When code and documentation disagree:

```text
actual source code + tests
> subsystem source-of-truth docs
> docs/CODEX_HANDOFF.md current-state summary
> README.md
```

Do not silently change architecture policy just because a document appears stale. First inspect the code, inspect tests, decide which document is stale, and report whether fixing it is in scope.

## Handoff Maintenance Rule

When a verified phase commit is completed, update [docs/CODEX_HANDOFF.md](docs/CODEX_HANDOFF.md) before asking another Codex conversation to continue. Keep it concise: current state, next step, important decisions, and links to source-of-truth docs.
