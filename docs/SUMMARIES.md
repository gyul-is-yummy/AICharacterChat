# Conversation Summaries

Conversation Summary stores a user-selected continuous message range inside a single `ChatSession`.

It is not automatic memory. It does not delete or rewrite original messages.

## Domain Model

`ChatSession` owns:

```text
Messages
Summaries
```

Each `ConversationSummary` stores:

```text
Id
StartMessageId
EndMessageId
Title
CurrentSituation
KeyEvents
RelationshipChanges
PromisesAndImportantStatements
UnresolvedMatters
PersistentState
CreatedAt
UpdatedAt
Revision
```

`Id`, `StartMessageId`, and `EndMessageId` are `Guid` values. The summary range is inclusive.

The six section fields are structured properties, not one free-form content field. Empty sections are represented as:

```text
없음
```

## Range Rules

Summary ranges must refer to messages in the same `ChatSession.Messages` list.

Validation requires:

- Summary id is not already used in the same session.
- Start message exists.
- End message exists.
- Start index is before or equal to end index.
- Title is non-empty.
- Title is at most 40 characters.
- Saved summaries do not overlap.

Adjacent and gapped ranges are allowed:

```text
1-5, 6-20
1-5, 10-20
```

Overlapping ranges are rejected:

```text
1-10, 8-20
```

## Historical Context

Historical Context is not persisted. It is built at request time from:

```text
ChatSession.Messages
ChatSession.Summaries
RecentMessageSelector result
```

Recent messages remain actual `ChatCompletionMessage` values. The current recent limit remains:

```text
MaxRecentMessages = 20
```

Historical Context contains messages outside the actual recent selection.

If a summary range is fully outside the recent selection, that range is rendered as a summary. If the summary overlaps recent messages, it is not used for compression yet, and the historical part of that range remains raw.

If persisted or externally edited summary data is invalid at render time, `HistoricalContextBuilder` does not mutate or delete it. Missing start/end messages, reversed ranges, and overlapping summary ranges are ignored for compression, and the covered historical messages fall back to raw rendering without message loss or duplication.

Summary titles, summary section values, and raw historical message contents are escaped before being inserted into the historical context text.

Raw historical messages are rendered as past reference material and do not use the current user message wrapping format. Recent user messages still use the existing `[현재 상황 서술]` request wrapping.

The historical renderer interleaves summary blocks and raw message blocks in conversation order.

## Clear Chat

Clear Chat clears both `ChatSession.Messages` and `ChatSession.Summaries` for the current character session.

If saving fails, the in-memory messages and summaries are restored and the failure is propagated instead of leaving a partially cleared session.

## Summary Draft Generation

`ConversationSummarizer` can generate a `SummaryDraft` from a selected continuous `ChatMessage` range.

`SummaryDraft` contains:

```text
StartMessageId
EndMessageId
Title
CurrentSituation
KeyEvents
RelationshipChanges
PromisesAndImportantStatements
UnresolvedMatters
PersistentState
```

It does not contain persistence metadata such as `Id`, `CreatedAt`, `UpdatedAt`, or `Revision`.

Draft generation is read-only:

```text
ChatSession.Messages is not mutated.
ChatSession.Summaries is not mutated.
Repository SaveAsync is not called.
```

Range validation reuses `ConversationSummaryService.ValidateRange`. New summary generation rejects overlap with existing summaries. Regeneration uses the selected `ConversationSummary` target object, keeps its original range, and internally ignores only that target summary so that overlap with other summaries is still rejected.

The AI request source is only the selected raw `ChatSession.Messages` range. It does not include lore, historical context, existing summaries, world settings, character prompt fields, or current-chat user wrapping. `Character.Name` is used only as the assistant speaker label in the transcript.

The selected transcript is rendered with XML-like delimiters, and transcript data values are escaped at request time. Raw domain message content is not encoded or saved.

The AI response is requested with a provider-independent structured JSON schema containing exactly:

```text
title
currentSituation
keyEvents
relationshipChanges
promisesAndImportantStatements
unresolvedMatters
persistentState
```

All fields are required strings and `additionalProperties` is false. The schema does not use `maxLength`; title length is validated in Application using the same 40-character rule as persisted summaries.

Empty summary section values are normalized to:

```text
없음
```

Empty titles, titles longer than 40 characters, JSON parse failures, API failures, and cancellations all return a generation result without changing domain data.

## Summary Persistence

`ConversationSummaryPersistenceService` applies approved summary changes to a `ChatSession` and saves the containing `WorldStore`.

```text
SummaryDraft
-> ConversationSummaryService domain mutation
-> IWorldRepository.SaveAsync
-> success result
```

The service is responsible for memory atomicity:

- Add creates a new `ConversationSummary` from a draft, saves once, and returns the created summary.
- Update mutates the selected summary object, saves once, and returns the same target object.
- Delete removes the selected summary object, saves once, and returns the removed object.

If validation fails, repository save is not called. If save fails or is canceled, the in-memory `ChatSession.Summaries` state is rolled back to the state before the operation. Rollback does not call `SaveAsync` again.

Edit keeps the summary range immutable. To change a range, the existing summary must be deleted and a new summary must be created.

The persistence service validates that the `ChatSession` belongs to the supplied `WorldStore` by object reference. Update and delete also target the exact selected `ConversationSummary` object by reference, not by searching for the first matching summary id.

Disk atomicity remains the responsibility of `JsonWorldRepository` and `JsonFileWriter`, which write through a temporary file and replace or move the final JSON file.

Duplicate `ConversationSummary.Id` values from externally edited JSON are not automatically repaired yet. Validation/recovery remains deferred until before Summary Management UI.

## Message Range Selection

Message range selection is Presentation-only UI state. It is not stored in `WorldStore`, `ChatSession`, `ChatMessage`, or JSON.

The chat screen projects domain messages into `ChatMessageItemViewModel` instances for rendering selection state. The source of truth remains `SelectedChatSession.Messages`.

Selection mode uses message clicks to choose one contiguous inclusive range. The first click sets the anchor, the second click sets the other endpoint, and reverse order clicks are normalized into chronological `StartMessageId` and `EndMessageId` values. Clicking another message after a completed range starts a new selection.

Range validation reuses `ConversationSummaryService.ValidateRange`, so existing summary overlap rules are not duplicated in Presentation. Invalid ranges remain highlighted, but `SummarySelectionError` is shown and the selection is not considered valid.

Sending a new chat message is disabled while selection mode is active. Session changes and Clear Chat clear the transient selection state.

3-B.3 does not call AI summary generation, open a preview, or save summaries.

## Old Unsummarized Count

`HistoricalContextBuilder` counts only historical raw messages that were not replaced by a summary.

The warning threshold is:

```text
OldUnsummarizedWarningThreshold = 20
```

3-A does not show a WPF warning banner yet. It only exposes the count and warning boolean through context metadata.

## Not Implemented Yet

The following are intentionally not implemented yet:

- Summary preview window
- Summary management window
- Warning banner UI
- Regeneration UI
- Direct edit UI
- Delete UI
- Automatic summary
- Long-term memory
- Embeddings, vector DB, semantic search
- Token budget
