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

## Old Unsummarized Count

`HistoricalContextBuilder` counts only historical raw messages that were not replaced by a summary.

The warning threshold is:

```text
OldUnsummarizedWarningThreshold = 20
```

3-A does not show a WPF warning banner yet. It only exposes the count and warning boolean through context metadata.

## Not Implemented In 3-A

The following are intentionally not implemented yet:

- AI summary generation
- Summary draft
- Summary preview window
- Summary management window
- Message range selection UI
- Warning banner UI
- Regeneration UI
- Direct edit UI
- Delete UI
- Automatic summary
- Long-term memory
- Embeddings, vector DB, semantic search
- Token budget
