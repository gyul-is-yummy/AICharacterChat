# Codex Handoff

This file records the current verified development state for a new Codex conversation or another device. It is not a replacement for the source-of-truth documentation linked below.

## Last Code-Verified Baseline

Last code-verified baseline commit:

```text
5ac12bd91de3b29d28a89f37057b99ca66b25bec
```

This is the last commit whose application source state was verified with Build, Tests, and the required manual UI smoke test. It is not necessarily the current repository HEAD.

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

Repository HEAD may be newer than this baseline. If every commit after the Last Code-Verified Baseline is documentation-only, the code-verified baseline remains valid and a newer HEAD alone is not an error.

Before continuing work on another device:

1. Check the current HEAD.
2. Inspect commits after the Last Code-Verified Baseline.
3. Distinguish documentation-only changes from application source changes.

If application source changed after the Last Code-Verified Baseline, do not assume the previous Build/Test result still verifies the current code. Run the appropriate build/tests and any required manual verification, then establish a new code-verified baseline before treating the newer source state as verified.

Do not record a documentation-only commit as the new Last Code-Verified Baseline merely because it is repository HEAD. The handoff tracks the verified application-code state to avoid a self-reference cycle where editing this file makes the recorded HEAD immediately stale.

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
- Phase 3-B.6 Unsummarized Warning

Phase 3-B.6 is implemented in the current working tree but is not committed yet.

## Important Current Decisions

- `ChatSession.Messages` remains the full persisted conversation history.
- Recent message selection and historical context are request-time projections.
- `ChatSession.Summaries` stores user-managed summary ranges and never deletes or mutates raw messages.
- Summary generation and regeneration are draft-only until the user explicitly saves.
- Summary update/delete/regenerate operations target exact `ConversationSummary` object references.
- Persisted summary ids are repaired at the JSON load boundary so each successful `ChatSession` load has non-empty, session-local unique summary ids.
- Summary Management supports manual edit, AI regenerate, and delete for saved summaries.
- Unsummarized Warning is a passive WPF notice derived from Application context semantics; it does not block send or automatically create summaries.

## Current Working Tree Verification

The current working tree contains uncommitted application source changes for Phase 3-B.6.

Build:

```text
0 warnings
0 errors
```

Tests:

```text
235 discovered
235 passed
0 failed
0 skipped
```

This verifies the uncommitted working tree only. Commit this source state before updating the Last Code-Verified Baseline hash.

Detailed behavior lives in:

- [Architecture](ARCHITECTURE.md)
- [Context](CONTEXT.md)
- [Data model](DATA_MODEL.md)
- [Conversation summaries](SUMMARIES.md)

## Next Phase

```text
Audit and commit Phase 3-B.6
Unsummarized Warning
IMPLEMENTED IN WORKING TREE
```

Do not begin a later feature phase until this working tree is verified and committed, unless the user explicitly redirects.

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
git diff --name-status 5ac12bd91de3b29d28a89f37057b99ca66b25bec..HEAD
```

If commits after the Last Code-Verified Baseline include application source changes, run:

```bash
dotnet build AICharacterChat.sln
dotnet test AICharacterChat.sln --no-build --verbosity normal
```

If the Last Code-Verified Baseline cannot be found, or if newer application source changes have not been verified yet, report that before changing code.

## Required Reading

1. [../AGENTS.md](../AGENTS.md)
2. [ARCHITECTURE.md](ARCHITECTURE.md)
3. [CONTEXT.md](CONTEXT.md)
4. [DATA_MODEL.md](DATA_MODEL.md)
5. [SUMMARIES.md](SUMMARIES.md)

Read [MIGRATION.md](MIGRATION.md) before touching legacy persistence behavior.
