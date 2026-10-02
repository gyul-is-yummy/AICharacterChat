# Architecture

AICharacterChat is still a single WPF application, organized into layered folders rather than separate class library projects.

```text
Domain
  Enums
  Models
Application
  Chat
  Context
  Interfaces
  Models
Infrastructure
  AI/Anthropic
  Persistence
Presentation
  ViewModels
```

## Responsibilities

- `Domain`: data and domain objects only.
- `Application`: provider-independent chat flow, prompt creation, lore matching, and interfaces.
- `Application/Context`: provider-independent request context assembly and recent message selection.
- `Infrastructure`: Anthropic HTTP client and JSON file persistence.
- `Presentation`: WPF windows and view models.

## Chat Flow

```text
raw user input
-> temporary User ChatMessage in ChatSession
-> ContextBuilder
   -> LoreMatcher, using the current raw user input
   -> PromptBuilder
   -> RecentMessageSelector
   -> HistoricalContextBuilder
   -> provider-independent request messages, with user role messages wrapped for the request
-> IChatModelClient.SendAsync
-> on success, append Assistant ChatMessage
-> save WorldStore once
```

On failure or cancellation, the temporary user message is removed, no assistant message is added, and the repository is not saved.

If the provider succeeds but repository save fails or is canceled, `ChatService` rolls back every message it added during the send operation.

`ChatSession.Messages` keeps the full conversation history. `RecentMessageSelector` only limits the messages sent in the AI request. It does not delete, summarize, or persist a reduced history.

`ChatSession.Summaries` stores user-managed `ConversationSummary` ranges. `HistoricalContextBuilder` uses summaries only when their full range is outside the actual recent selection; otherwise the historical portion remains raw. Historical Context is rendered on demand and is not saved.

## Summary Draft Generation

`ConversationSummarizer` creates an editable `SummaryDraft` from a selected continuous `ChatMessage` range.

```text
selected raw ChatSession.Messages range
-> ConversationSummarizer
-> IChatModelClient
-> structured JSON response
-> SummaryDraft
```

Summary draft generation is read-only. It does not mutate `ChatSession.Messages`, does not add to `ChatSession.Summaries`, and does not save repositories.

The summary request uses only the selected raw message range plus `Character.Name` as the assistant speaker label. It does not use `ContextBuilder`, `PromptBuilder`, `LoreMatcher`, `HistoricalContextBuilder`, existing summaries, world settings, or character prompt fields.

Structured responses are represented in Application as provider-independent `JsonSchemaResponseFormat`. Anthropic-specific `output_config.format` mapping remains in Infrastructure.

## Summary Persistence Flow

Approved summary changes are handled by `ConversationSummaryPersistenceService`:

```text
SummaryDraft or selected ConversationSummary
-> ConversationSummaryService
-> IWorldRepository.SaveAsync(WorldStore)
```

`ConversationSummaryService` remains a domain validation and mutation service. It does not depend on repositories.

`ConversationSummaryPersistenceService` owns memory rollback for Add, Update, and Delete. If repository save fails or is canceled, it restores `ChatSession.Summaries` to the pre-operation state and does not retry saving. Update rollback restores the same target object instead of replacing it, and Delete rollback reinserts the same object at its original index.

`JsonWorldRepository` and `JsonFileWriter` remain responsible for disk-level atomicity. The summary persistence flow does not introduce a new repository abstraction, unit-of-work layer, lock, or storage format.

Summary generation and regeneration remain draft-only. Regeneration targets an existing `ConversationSummary` object, uses its original message range, and does not mutate or save the target summary.

## Summary Identity Load Normalization

`JsonWorldRepository.LoadAsync` normalizes persisted summary identity before returning a `WorldStore` to Presentation. After deserialization or legacy migration and runtime defaults, each `ChatSession.Summaries` list is repaired so that `ConversationSummary.Id` values are non-empty and unique within that session.

The repair is an Infrastructure persistence detail. It does not call `ConversationSummaryService`, does not expose a public Application API, and does not run from `MainViewModel` or Summary Management UI.

Only identity corruption is repaired. Duplicate ids keep the first valid occurrence and reassign later duplicates; `Guid.Empty` is reassigned. Summary order, message range, content sections, timestamps, and revision are preserved. Different sessions may still contain the same summary id because the invariant is session-local.

If repair changes any id, `JsonWorldRepository` persists the repaired store through the existing `SaveAsync` path, which uses `JsonFileWriter` for atomic file replacement. If no identity repair occurs, current-format load does not save only for this normalization step.

## Summary Range Selection

Message range selection lives in Presentation. `MainViewModel` owns the transient selection mode, selected message references, validation state, and commands. `ChatMessageItemViewModel` is a rendering projection over each domain `ChatMessage`; it carries only visual selection flags.

The domain source of truth remains `SelectedChatSession.Messages`. Selection state is not persisted. The selected range is exposed as `StartMessageId` and `EndMessageId` for the later summary draft step.

Range validation reuses `ConversationSummaryService.ValidateRange`. The selection UI does not call `ConversationSummarizer`, does not save summaries, and does not use the summary persistence workflow.

## Summary Preview Flow

Summary Preview connects the selected message range to user-controlled summary creation.

```text
MainViewModel valid range
-> SummaryPreviewRequested event
-> MainWindow opens modal SummaryPreviewWindow
-> SummaryPreviewViewModel.GenerateNewDraftAsync
-> editable preview fields
-> ConversationSummaryPersistenceService.AddAsync on Save
```

`MainViewModel` does not construct WPF windows. It raises a Presentation event with a `SummaryPreviewRequest` that captures the current `WorldStore`, `ChatSession`, `Character`, selected range ids, and model id. `MainWindow`, as the composition root, creates the preview ViewModel and modal window.

`SummaryPreviewViewModel` depends on `ConversationSummarizer`, `ConversationSummaryPersistenceService`, and the captured request. It does not depend on Anthropic, repositories, or WPF window types directly.

Initial generation and preview regeneration use `ConversationSummarizer.GenerateNewDraftAsync` and remain read-only. Edited preview fields are Presentation-only state until Save. Save is the only point where `ChatSession.Summaries` may change, and it goes through `ConversationSummaryPersistenceService.AddAsync`.

Canceling the modal preview preserves the selected range in `MainViewModel`. A successful Save returns `Saved` to `MainViewModel`, which clears the transient selection state without refreshing messages.

## Summary Management Flow

Summary Management provides a modal editor for summaries already saved in the current `ChatSession`.

```text
MainViewModel current session
-> SummaryManagementRequested event
-> MainWindow opens modal SummaryManagementWindow
-> SummaryManagementViewModel editable buffer
-> ConversationSummarizer.RegenerateDraftAsync on AI regenerate
-> ConversationSummaryPersistenceService.UpdateAsync on Save
-> ConversationSummaryPersistenceService.DeleteAsync on Delete
```

`MainViewModel` does not construct the management window. It raises a Presentation event with a `SummaryManagementRequest` that captures the current `WorldStore`, `ChatSession`, `Character`, and model id. `MainWindow`, as the composition root, creates `SummaryManagementViewModel` and the modal window.

`SummaryManagementViewModel` depends on `ConversationSummaryPersistenceService`, `ConversationSummarizer`, and the captured request. It does not depend on Anthropic or WPF window types directly. `MainWindow` passes the same `ConversationSummarizer` and summary persistence service used by the Preview flow; it does not create a second provider client, repository, or summarizer graph.

`SummaryManagementItemViewModel` wraps a persisted `ConversationSummary` as a Presentation-only list item. It preserves the exact summary object reference so updates are applied to the selected object through `ConversationSummaryPersistenceService.UpdateAsync`.

Editable fields are Presentation state until Save succeeds. Dirty state blocks changing the selected summary, and the window asks for confirmation before discarding unsaved edits on close.

AI regeneration targets the selected item's exact `ConversationSummary` reference and uses the `ModelId` captured in `SummaryManagementRequest`. The result is copied only into the editable buffer and is never saved automatically. Dirty regeneration asks for confirmation before replacing the current buffer.

Regeneration owns a per-operation cancellation token source and uses operation identity checks before applying success or failure results. Closing the modal cancels active regeneration, and late success/failure from a provider that ignored cancellation is ignored. Save and Delete close attempts are blocked while persistence is in progress.

Delete also targets the selected item by object reference and delegates persistence to `ConversationSummaryPersistenceService.DeleteAsync`. On success the wrapper is removed, selection is cleared, and raw chat messages remain unchanged.

Invalid persisted summary ranges are not repaired by Presentation. They stay visible and can still be manually edited, saved, or deleted, but AI regeneration is disabled because it requires the original raw message range.

## Current Boundaries

`MainWindow` is MVVM-backed through `MainViewModel`. The code-behind constructs dependencies, opens dialogs, handles Enter-to-send, and scrolls the chat view.

`LoreBookWindow` and `UserProfileManagerWindow` use dedicated ViewModels for collection mutation and save coordination. `CharacterSettingsWindow` still contains WPF row-building code for custom fields and relationships, but selected persona/session state is returned to `MainViewModel` instead of being written by the window.

`MainWindow` is the current composition root and constructs the concrete Anthropic and JSON infrastructure objects. Presentation ViewModels depend on Application interfaces, not Anthropic implementation types.
