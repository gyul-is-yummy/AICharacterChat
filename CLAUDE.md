# CLAUDE.md

This repository is a Korean-language WPF character chat app using Anthropic Claude.

## Build And Test

```bash
dotnet build AICharacterChat.sln
dotnet test AICharacterChat.sln
```

- Current target framework: `net8.0-windows`
- .NET 10 migration is intentionally not applied because this machine only has SDK `9.0.302`
- Main packages: `Newtonsoft.Json`, `CommunityToolkit.Mvvm`
- Tests use xUnit with `Microsoft.NET.Test.Sdk` and are discovered by `dotnet test`

## Architecture

The app remains a single WPF project, but code is organized by responsibility:

```text
AICharacterChat/
  Domain/
    Enums/
    Models/
  Application/
    Chat/
    Interfaces/
    Models/
  Infrastructure/
    AI/Anthropic/
    Persistence/
  Presentation/
    ViewModels/
```

Core flow:

```text
MainViewModel
-> ChatService
-> LoreMatcher
-> PromptBuilder
-> IChatModelClient
-> IWorldRepository
```

`MainWindow.xaml.cs` should stay limited to view lifecycle, dialog opening, Enter-key behavior, and scrolling. Do not move API calls, prompt construction, JSON serialization, or chat-state decisions back into code-behind.

## Data Model

Current hierarchy:

```text
WorldStore
  Worlds[]
    Characters[]
    UserPersonas[]
    ChatSessions[]
      Messages[]
```

Important model rules:

- `Character` contains character configuration only.
- `Character.DefaultScenario` is the migrated form of legacy `CharacterProfile.Situation`.
- `ChatSession` owns `UserPersonaId`, optional `Scenario`, and `Messages`.
- `ChatSession.Scenario` overrides `Character.DefaultScenario` only when non-empty.
- `ChatMessage.Role` uses `ChatRole` enum.
- User message `Content` must remain the raw user input. Provider-specific wrapping happens only when building the AI request.
- `World` owns chat sessions for now. Do not add `IChatSessionRepository` unless chat storage is intentionally split later.

## Persistence

New data location:

```text
%LocalAppData%/AICharacterChat/
  settings.json
  data/worlds.json
```

Persistence classes:

- `JsonWorldRepository`: loads/saves `WorldStore`
- `JsonSettingsRepository`: loads/saves `AppSettings`
- `LegacyDataMigrator`: converts legacy executable-adjacent `worlds.json`
- `AppDataPaths`: centralizes paths

Writes use a temporary file and replace/move into place. Loading must not deduplicate or mutate user data silently. Data changes should happen through explicit actions or migration.

Writes are serialized through the shared JSON writer and use per-save temporary file names to avoid concurrent save collisions.

## Anthropic

Anthropic integration lives under `Infrastructure/AI/Anthropic`.

- API key comes from `ANTHROPIC_API_KEY`
- Models live in `AnthropicModelCatalog`
- `AnthropicClient` implements `IChatModelClient`
- Do not use `dynamic` response parsing
- Do not call `HttpClient.DefaultRequestHeaders.Clear()` per request
- API failures throw typed exceptions and must not be saved as assistant messages

## Migration Rules

Legacy conversion rules:

```text
CharacterProfile.Id -> Character.Id
CharacterProfile.Situation -> Character.DefaultScenario
CharacterProfile.ConversationHistory -> default ChatSession.Messages
CharacterProfile.SelectedUserProfileId -> default ChatSession.UserPersonaId
UserProfile -> UserPersona
```

For migrated sessions:

```text
ChatSession.Scenario = ""
```

Legacy wrapped user messages are unwrapped using the old `[현재 상황 서술]` format. Repeated messages are preserved; there is no `role + first 50 chars` deduplication in the new loader.

## Tests

The xUnit test suite covers:

- `PromptBuilderTests`
- `LoreMatcherTests`
- `ChatServiceTests`
- `JsonWorldRepositoryTests`
- `LegacyDataMigratorTests`
- `MainViewModelTests`

`ChatServiceTests` use a fake `IChatModelClient`; tests must not call the real Anthropic API.

## Remaining Notes

- Do not upgrade to `net9.0-windows` as an intermediate target.
- `net10.0-windows` can be revisited only after the .NET 10 SDK is installed.
- `LoreBookWindow` and `UserProfileManagerWindow` delegate collection mutation and saving to ViewModels.
- `CharacterSettingsWindow` still uses code-behind for WPF-only dynamic custom-field and relationship rows. Keep persistence and session state changes in ViewModels.
