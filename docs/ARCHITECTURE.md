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
   -> provider-independent request messages, with user role messages wrapped for the request
-> IChatModelClient.SendAsync
-> on success, append Assistant ChatMessage
-> save WorldStore once
```

On failure or cancellation, the temporary user message is removed, no assistant message is added, and the repository is not saved.

If the provider succeeds but repository save fails or is canceled, `ChatService` rolls back every message it added during the send operation.

`ChatSession.Messages` keeps the full conversation history. `RecentMessageSelector` only limits the messages sent in the AI request. It does not delete, summarize, or persist a reduced history.

## Current Boundaries

`MainWindow` is MVVM-backed through `MainViewModel`. The code-behind constructs dependencies, opens dialogs, handles Enter-to-send, and scrolls the chat view.

`LoreBookWindow` and `UserProfileManagerWindow` use dedicated ViewModels for collection mutation and save coordination. `CharacterSettingsWindow` still contains WPF row-building code for custom fields and relationships, but selected persona/session state is returned to `MainViewModel` instead of being written by the window.

`MainWindow` is the current composition root and constructs the concrete Anthropic and JSON infrastructure objects. Presentation ViewModels depend on Application interfaces, not Anthropic implementation types.
