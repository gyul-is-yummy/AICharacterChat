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

## Historical Compression

Historical Context is not persisted. It is built at request time from:

```text
ChatSession.Messages
ChatSession.Summaries
RecentMessageSelector result
```

Recent messages remain actual `ChatCompletionMessage` values. The current recent limit is:

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

## AI Draft Generation

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

## Persistence

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

## Identity Invariant and Repair

`ConversationSummary.Id` is a session-local persisted identity. After `JsonWorldRepository.LoadAsync` succeeds, every `ConversationSummary.Id` in a single `ChatSession.Summaries` list is non-empty and unique within that session.

If externally edited JSON contains duplicate summary ids or `Guid.Empty` summary ids, the persistence load boundary repairs only the affected ids. The first valid occurrence in each session keeps its id, later duplicates receive new `Guid` values, and `Guid.Empty` values receive new non-empty `Guid` values.

The repair preserves summary order, range, title, section content, timestamps, and revision.

When identity repair changes data during load, the repaired `WorldStore` is immediately persisted through the normal `JsonWorldRepository.SaveAsync` and `JsonFileWriter` atomic write path. Normal current-format loads with no identity repair are not rewritten for this reason.

Duplicate `ChatMessage.Id` validation/recovery remains a separate backlog item.

## Message Range Selection

Message range selection is Presentation-only UI state. It is not stored in `WorldStore`, `ChatSession`, `ChatMessage`, or JSON.

The chat screen projects domain messages into `ChatMessageItemViewModel` instances for rendering selection state. The source of truth remains `SelectedChatSession.Messages`.

Selection mode uses message clicks to choose one contiguous inclusive range. The first click sets the anchor, the second click sets the other endpoint, and reverse order clicks are normalized into chronological `StartMessageId` and `EndMessageId` values. Clicking another message after a completed range starts a new selection.

Range validation reuses `ConversationSummaryService.ValidateRange`, so existing summary overlap rules are not duplicated in Presentation. Invalid ranges remain highlighted, but `SummarySelectionError` is shown and the selection is not considered valid.

Sending a new chat message is disabled while selection mode is active. Session changes and Clear Chat clear the transient selection state.

## Preview

After a valid range is selected, the chat screen can open a modal Summary Preview workflow.

```text
Message range selection
-> SummaryPreviewRequest
-> modal preview window
-> AI draft generation
-> editable preview fields
-> explicit Save
-> ConversationSummaryPersistenceService.AddAsync
```

`SummaryPreviewRequest` captures the current `WorldStore`, `ChatSession`, `Character`, selected start/end message ids, and the selected model id when the user clicks Next.

Initial generation and regeneration both use `ConversationSummarizer.GenerateNewDraftAsync` with the captured message range and model id. `RegenerateDraftAsync` remains reserved for existing saved summaries.

AI generation and regeneration remain read-only. They do not mutate `ChatSession.Messages`, do not add to `ChatSession.Summaries`, and do not save repositories.

The preview fields are editable Presentation state:

```text
Title
CurrentSituation
KeyEvents
RelationshipChanges
PromisesAndImportantStatements
UnresolvedMatters
PersistentState
```

Saving is user-controlled. AI output is never auto-saved. Only clicking Save builds a new `SummaryDraft` from the edited fields and calls `ConversationSummaryPersistenceService.AddAsync`.

If preview generation fails, the preview remains open and the selected range in the main window is preserved. If regeneration fails, the current editable fields are preserved. If save fails, the preview remains open, edited fields are preserved, and persistence rollback is handled by `ConversationSummaryPersistenceService`.

Canceling the preview or closing it with X does not persist anything and returns to the existing message range selection. Saving successfully closes the preview and clears the transient selection state.

## Management

The chat screen can open a modal Summary Management workflow for the current `ChatSession`.

```text
MainViewModel
-> SummaryManagementRequest
-> modal management window
-> editable Presentation buffer
-> ConversationSummarizer.RegenerateDraftAsync on AI regenerate
-> ConversationSummaryPersistenceService.UpdateAsync on Save
-> ConversationSummaryPersistenceService.DeleteAsync on Delete
```

`SummaryManagementRequest` captures the current `WorldStore`, `ChatSession`, `Character`, and selected model id. The management workflow edits only summaries that already exist in the captured session.

`SummaryManagementItemViewModel` is a Presentation-only wrapper around a persisted `ConversationSummary`. It keeps the exact domain object reference so update, regenerate, and delete target the selected summary object, not the first matching id.

The editor uses a separate editable buffer for:

```text
Title
CurrentSituation
KeyEvents
RelationshipChanges
PromisesAndImportantStatements
UnresolvedMatters
PersistentState
```

Changing these fields does not mutate the domain summary until Save succeeds. Save builds a `SummaryDraft` from the buffer, preserves the summary range, and calls `ConversationSummaryPersistenceService.UpdateAsync`.

AI regeneration uses the selected item's exact `ConversationSummary` reference and the `ModelId` captured when the management window opened. It calls `ConversationSummarizer.RegenerateDraftAsync`, which uses the summary's original `StartMessageId` and `EndMessageId` to read raw `ChatSession.Messages`.

Regeneration does not auto-save. A successful AI result is applied only to the editable buffer, so `IsDirty` becomes true when the draft differs from the persisted summary. The user must click Save before the persisted `ConversationSummary` changes.

If unsaved edits exist before regeneration, the window asks whether the AI result may replace the current buffer. If regeneration fails, is canceled, or returns after the window has closed, the current buffer, selected summary, persisted domain object, and error state are not overwritten by stale results.

The management window validates whether the selected summary's persisted range can be regenerated. Corrupted ranges, missing endpoints, reversed ranges, or overlap corruption leave the summary visible and still allow manual edit, Save, and Delete, but disable AI regeneration with an explanatory message. The UI does not repair, rewrite, or delete invalid ranges automatically.

Delete uses the selected item's exact `ConversationSummary` reference and calls `ConversationSummaryPersistenceService.DeleteAsync`. Delete asks for confirmation and notes that raw conversation messages are not removed. If unsaved edits exist, the confirmation also states that the unsaved edits will be discarded.

On delete success, the target summary and its wrapper are removed, the editor buffer is cleared, and selection becomes null even if other summaries remain. If no summaries remain, the empty state is shown. If summaries remain, the window asks the user to select one from the list. Delete failure or cancellation preserves the wrapper, selection, editable buffer, and domain state, with an error message.

While a selected summary has unsaved edits, selecting another summary is blocked and closing the window asks whether to discard changes. Save and Delete block closing while persistence is in progress. AI regeneration does not block closing; if the window closes, active regeneration is canceled and late success/failure is ignored.

Dirty or busy state blocks changing the selected summary through ViewModel guards and item-level hit-test/focus locks, while keeping the `ListBox` itself enabled so the list can still scroll.

## Unsummarized Metadata

`HistoricalContextBuilder` counts only historical raw messages that were not replaced by a summary.

The warning threshold is:

```text
OldUnsummarizedWarningThreshold = 20
```

The backend exposes the count and warning boolean through context metadata. WPF UI for Unsummarized Warning is not implemented yet.

## Not Implemented Yet

The following are intentionally not implemented yet:

- Unsummarized Warning UI
- duplicate `ChatMessage.Id` recovery
- invalid / reversed Summary range repair
- overlapping Summary range repair
- Long-term Memory
- automatic Summary generation
- global Summary concurrency coordination
- embeddings, vector DB, semantic search
