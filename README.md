# AICharacterChat

AICharacterChat is a Korean-language WPF desktop app for character-based AI chat. It stores worlds, characters, user personas, chat sessions, messages, and user-managed conversation summaries in local JSON files.

## Overview

The app lets a user create worlds and characters, chat with a selected character through an Anthropic chat model, and preserve long conversations with explicit conversation summaries. The current architecture keeps domain data, chat orchestration, provider integration, persistence, and WPF presentation concerns separated inside one WPF solution.

## Tech Stack

- .NET WPF targeting `net8.0-windows`
- CommunityToolkit.Mvvm
- Newtonsoft.Json
- Anthropic API integration
- xUnit test project

## Build & Test

```bash
dotnet build AICharacterChat.sln
dotnet test AICharacterChat.sln
```

The test suite uses fake chat-model clients where appropriate. Tests must not call the real Anthropic API.

## Configuration

Set the Anthropic API key inside the app:

```text
Run the app -> API 설정 -> enter Anthropic API Key -> 저장 / 교체
```

The API key is not stored in `settings.json`. It is saved as a Windows-user-protected local credential.

Runtime data is stored under:

```text
%LocalAppData%/AICharacterChat/
  credentials.dat
  settings.json
  data/worlds.json
```

Do not commit personal `settings.json`, `worlds.json`, API keys, `bin/`, `obj/`, or temporary test output.

## Architecture

The app is organized by responsibility:

```text
AICharacterChat/
  Domain/
  Application/
  Infrastructure/
  Presentation/
```

For the detailed system structure, see [Architecture](docs/ARCHITECTURE.md).

## Documentation

- [Agent development rules](AGENTS.md)
- [Current Codex handoff](docs/CODEX_HANDOFF.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Context semantics](docs/CONTEXT.md)
- [Data model](docs/DATA_MODEL.md)
- [Migration](docs/MIGRATION.md)
- [Conversation summaries](docs/SUMMARIES.md)

## Development with Codex

Before continuing work in a new Codex conversation or on another device, read [AGENTS.md](AGENTS.md) and [docs/CODEX_HANDOFF.md](docs/CODEX_HANDOFF.md). `AGENTS.md` contains long-term development rules; `CODEX_HANDOFF.md` contains the latest verified project state.
