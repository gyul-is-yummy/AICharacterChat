# Architecture

AICharacterChat is a single WPF application organized by responsibility rather than by separate class library projects.

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
  Models
  ViewModels
```

## Layer Responsibilities

- `Domain`: data and domain objects.
- `Application`: provider-independent chat orchestration, context building, summary services, request/response models, and interfaces.
- `Infrastructure`: Anthropic HTTP integration and JSON file persistence.
- `Presentation`: WPF windows, presentation models, and ViewModels.

`MainWindow` is the current composition root. It constructs concrete infrastructure objects, wires application services, opens modal windows, handles WPF lifecycle events, handles Enter-to-send behavior, and scrolls the chat view.

Presentation ViewModels depend on Application services and interfaces. Application code must not depend on WPF windows or Anthropic implementation types.

## Chat Flow

```text
raw user input
-> temporary User ChatMessage in ChatSession
-> ChatService
-> ContextBuilder
-> IChatModelClient.SendAsync
-> on success, append Assistant ChatMessage
-> save WorldStore once
```

On API failure or cancellation, the temporary user message is removed, no assistant message is added, and the repository is not saved. If the provider succeeds but repository save fails or is canceled, `ChatService` rolls back the messages it added during the send operation.

Context assembly is described in [Context](CONTEXT.md).

## Persistence Composition

Runtime data is stored under `%LocalAppData%/AICharacterChat/`.

`JsonWorldRepository` loads and saves `WorldStore`. `JsonSettingsRepository` loads and saves `AppSettings`. `LegacyDataMigrator` converts old executable-adjacent `worlds.json` data when the new app-data store does not exist.

JSON writes go through the shared file writer with temporary files and atomic replacement/move behavior.

Legacy migration behavior is described in [Migration](MIGRATION.md).

## Summary Subsystem

Conversation summaries are stored on `ChatSession.Summaries` and are managed through Application services:

```text
ConversationSummarizer
ConversationSummaryService
ConversationSummaryPersistenceService
HistoricalContextBuilder
ConversationHistoryStatusService
```

High-level workflows:

```text
message range selection
-> Summary Preview
-> explicit Save
-> ConversationSummaryPersistenceService.AddAsync

saved summary selection
-> Summary Management
-> manual edit / AI regenerate / delete
-> ConversationSummaryPersistenceService.UpdateAsync or DeleteAsync
```

`MainViewModel` raises presentation events for Preview and Management. `MainWindow` opens the modal windows and supplies the shared services.

`ConversationHistoryStatusService` is an Application query service that reuses recent-message selection and historical context semantics to expose the current session's unsummarized warning status to Presentation.

Summary-specific rules, validation, persistence rollback, identity repair, preview behavior, management behavior, regenerate behavior, and delete behavior are defined in [Conversation summaries](SUMMARIES.md).

## Current WPF Boundaries

`MainWindow.xaml.cs` should stay limited to composition and WPF concerns. It should not own prompt construction, provider payload shaping, repository serialization, or business decisions.

`LoreBookWindow` and `UserProfileManagerWindow` use dedicated ViewModels for collection mutation and save coordination.

`CharacterSettingsWindow` still contains WPF row-building code for custom fields and relationships, but selected persona/session state is returned to `MainViewModel` rather than written by the window.
