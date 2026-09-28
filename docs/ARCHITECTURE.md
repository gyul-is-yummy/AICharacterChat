# Architecture

AICharacterChat is still a single WPF application, organized into layered folders rather than separate class library projects.

```text
Domain
  Enums
  Models
Application
  Chat
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
- `Infrastructure`: Anthropic HTTP client and JSON file persistence.
- `Presentation`: WPF windows and view models.

## Chat Flow

```text
raw user input
-> temporary User ChatMessage in ChatSession
-> LoreMatcher
-> PromptBuilder
-> provider request messages, with user input wrapped for Anthropic
-> IChatModelClient.SendAsync
-> on success, append Assistant ChatMessage
-> save WorldStore once
```

On failure or cancellation, the temporary user message is removed, no assistant message is added, and the repository is not saved.

If the provider succeeds but repository save fails or is canceled, `ChatService` rolls back every message it added during the send operation.

## Current Boundaries

`MainWindow` is MVVM-backed through `MainViewModel`. The code-behind constructs dependencies, opens dialogs, handles Enter-to-send, and scrolls the chat view.

`LoreBookWindow` and `UserProfileManagerWindow` use dedicated ViewModels for collection mutation and save coordination. `CharacterSettingsWindow` still contains WPF row-building code for custom fields and relationships, but selected persona/session state is returned to `MainViewModel` instead of being written by the window.

`MainWindow` is the current composition root and constructs the concrete Anthropic and JSON infrastructure objects. Presentation ViewModels depend on Application interfaces, not Anthropic implementation types.
