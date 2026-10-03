# Codex Handoff

This file records the current verified development state for a new Codex conversation or another device. It is not a replacement for the source-of-truth documentation linked below.

## Current Verified Baseline

Latest verified commit:

```text
5ac12bd91de3b29d28a89f37057b99ca66b25bec
```

Subject:

```text
feat: 저장된 대화 요약 재생성 및 삭제 기능 추가
```

Build:

```text
0 warnings
0 errors
```

Tests:

```text
223 discovered
223 passed
0 failed
0 skipped
```

Manual UI smoke test:

```text
passed
```

## Completed Phases

- Phase 1 Architecture Refactor
- Phase 2 ContextBuilder + Recent Messages
- Phase 3-A Summary Core + Historical Context
- Phase 3-A.1 Safety Hardening
- Phase 3-B.1 AI Summary Draft Generation
- Phase 3-B.2 Summary Persistence Transaction
- Phase 3-B.3 Message Range Selection
- Phase 3-B.4 Summary Preview
- Phase 3-B.4.1 Preview Lifecycle Stability
- Phase 3-B.5a Summary Identity Integrity
- Phase 3-B.5b Summary Management

Phase 3-B.5 is complete.

## Important Current Decisions

- `ChatSession.Messages` remains the full persisted conversation history.
- Recent message selection and historical context are request-time projections.
- `ChatSession.Summaries` stores user-managed summary ranges and never deletes or mutates raw messages.
- Summary generation and regeneration are draft-only until the user explicitly saves.
- Summary update/delete/regenerate operations target exact `ConversationSummary` object references.
- Persisted summary ids are repaired at the JSON load boundary so each successful `ChatSession` load has non-empty, session-local unique summary ids.
- Summary Management supports manual edit, AI regenerate, and delete for saved summaries.

Detailed behavior lives in:

- [Architecture](ARCHITECTURE.md)
- [Context](CONTEXT.md)
- [Data model](DATA_MODEL.md)
- [Conversation summaries](SUMMARIES.md)

## Next Phase

```text
Phase 3-B.6
Unsummarized Warning
NOT IMPLEMENTED
```

Do not begin this phase unless the user explicitly asks.

## Deferred Backlog

- duplicate `ChatMessage.Id` recovery
- invalid / reversed Summary range repair
- overlapping Summary range repair
- Long-term Memory
- automatic Summary generation
- global Summary concurrency coordination

## Before Continuing Work

Run these checks in the new environment:

```bash
git status --short
git log --oneline -5
dotnet build AICharacterChat.sln
dotnet test AICharacterChat.sln --no-build --verbosity normal
```

If the verified baseline differs, report it before changing code.

## Required Reading

1. [../AGENTS.md](../AGENTS.md)
2. [ARCHITECTURE.md](ARCHITECTURE.md)
3. [CONTEXT.md](CONTEXT.md)
4. [DATA_MODEL.md](DATA_MODEL.md)
5. [SUMMARIES.md](SUMMARIES.md)

Read [MIGRATION.md](MIGRATION.md) before touching legacy persistence behavior.
