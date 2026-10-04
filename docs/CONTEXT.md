# Context

`ContextBuilder` is the Application-layer component that decides what provider-independent context is sent to the chat model.

It receives the current `World`, `Character`, `ChatSession`, and raw user input, then builds a `BuiltChatContext` containing:

- `SystemPrompt`
- `Messages`
- request-time context metadata

It does not call HTTP APIs, create Anthropic DTOs, choose models, decide `MaxTokens`, save repositories, mutate chat sessions, add assistant messages, roll back state, or update UI.

## Flow

```text
ChatService
-> raw User ChatMessage is appended to ChatSession.Messages
-> ContextBuilder
   -> LoreMatcher.Match(character.Lore, rawUserInput)
   -> PromptBuilder.Build(...)
   -> RecentMessageSelector.Select(session.Messages)
   -> HistoricalContextBuilder.Build(session, recentMessages)
   -> ChatMessage values are converted to ChatCompletionMessage values
-> ChatCompletionRequest
-> IChatModelClient
```

## Recent Messages

`ChatSession.Messages` stores the full conversation history. The recent message window exists only while building the AI request.

`RecentMessageSelector` currently uses a simple count-based window:

```text
MaxRecentMessages = 20
```

This value is not exposed through UI or `settings.json`.

Selection policy:

- If the message count is less than or equal to the limit, all messages are returned in their original order.
- If the message count is greater than the limit, only the most recent messages are selected.
- The selected result remains ordered from oldest to newest.
- The source `ChatSession.Messages` and each `ChatMessage` are not modified.
- When truncation occurs and the selected window starts with one or more `Assistant` messages, those leading assistant messages are removed if the window contains a later `User` message.
- If truncated data contains no `User` message at all, the selected window is kept as-is to avoid deleting abnormal but potentially valuable data.

## Historical Context

Historical Context is computed at request time. It is not stored in Domain or JSON.

`ContextBuilder` runs `RecentMessageSelector` once, then shares that exact recent selection with `HistoricalContextBuilder` and the request message conversion step. The first actual recent message defines the historical boundary, so assistant messages trimmed from the recent window are preserved as historical raw messages.

Historical Context is appended after the base system prompt. It contains conversation history before the actual recent selection:

- Summary ranges fully outside the recent selection are rendered as summary blocks.
- Gaps without summaries are rendered as raw historical message blocks.
- Summary ranges that overlap recent messages are not used for compression yet.
- Invalid persisted summary ranges are not used for compression.
- Overlapping persisted summary ranges are not used for compression.
- Raw historical user messages are not wrapped with `[현재 상황 서술]`.
- Recent user messages keep the existing request wrapping.

Invalid or overlapping summaries are not repaired, deleted, or rewritten by `HistoricalContextBuilder`. Their messages fall back to raw historical rendering for that request, preserving chronological order without dropping or duplicating messages.

All user/AI data values inserted into Historical Context are escaped. This includes summary titles, the six summary section values, and raw historical message contents.

`HistoricalContextBuilder` also reports `UnsummarizedOldMessageCount` and a warning boolean. The warning threshold is currently 20 raw historical messages.

`ConversationHistoryStatusService` reuses `RecentMessageSelector` and `HistoricalContextBuilder` to expose the same count and warning state for the current session UI. Presentation must use this query result instead of calculating message counts independently.

Summary-specific rules are described in [Conversation summaries](SUMMARIES.md).

## Lore

`LoreMatcher` checks only the current raw user input. Recent messages and historical summaries are not used as lore search input.

The current implementation does not include semantic search, embeddings, lore priority, or past-conversation-based lore activation.

## User Message Wrapping

Domain user messages remain raw:

```text
Role = User
Content = "오늘 피곤해."
```

When request messages are created, every included `User` role message is wrapped using the existing request format:

```text
[현재 상황 서술]
{raw content}

위 상황에서 {CharacterName}으로서 반응해주세요.
행동 묘사와 대사를 함께 포함하여 소설 문체로 답하세요.
```

This wrapping happens only on the new `ChatCompletionMessage` values. It must not mutate `ChatMessage.Content`.

## Boundary

Summary creation, preview, management, regeneration, delete, and persistence are not `ContextBuilder` responsibilities. Context uses saved summaries only as request-time historical compression input.
