# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Collaboration Rules

- **Before making large changes:** If a task requires modifying many files or significant restructuring, stop and confirm the plan with me before proceeding.
- **Modularize by feature:** Do not put everything in one file. Split code into separate files organized by feature or responsibility.
- **Clarify before acting:** If a request is ambiguous or unclear, do not infer and execute. Instead, summarize your understanding of the request and ask me to confirm before doing anything.

## Build & Run

```bash
dotnet build AICharacterChat.sln
dotnet run --project AICharacterChat/AICharacterChat.csproj
```

- Target framework: `net8.0-windows` (Windows only — WPF)
- Single dependency: `Newtonsoft.Json` v13.0.4
- No test projects exist

## Architecture

This is a Korean-language romantic fantasy RPG chatbot built with WPF. Users create worlds and characters, then chat with AI-powered characters via the Anthropic Claude API.

**Data hierarchy:** `WorldManager` → `WorldProfile[]` → `CharacterProfile[]` → `ChatMessage[]` / `LoreEntry[]`

All data is persisted to `worlds.json` (written next to the executable at runtime) by `WorldManager.cs`, which handles serialization/deserialization and deduplication on load.

**Key files:**

| File | Role |
|---|---|
| `MainWindow.xaml.cs` | Orchestrator: chat UI, API calls, world/character switching |
| `CharacterProfile.cs` | Data model + generates the Claude system prompt from character fields |
| `WorldManager.cs` | JSON persistence for all worlds, characters, user profiles, and chat history |
| `CharacterSettingsWindow.xaml.cs` | Edit character attributes, relationships, custom fields |
| `WorldSettingsWindow.xaml.cs` | Edit world settings (genre, era, rules) |
| `UserProfileManagerWindow.xaml.cs` | Manage user personas (the player character) |
| `UserProfileSettingsWindow.xaml.cs` | Edit individual user profile fields |
| `LoreEntry.cs` | Lorebook entry model (Id, Title, Keywords, Content, IsEnabled) |
| `LoreBookWindow.xaml.cs` | Per-character lorebook manager (add/edit/delete/toggle entries) |
| `ChatMessage.cs` | API message model (role + content) |
| `CustomField.cs` | User-defined character attribute fields |
| `CharacterRelationship.cs` | Relationship data between two characters |
| `UserProfile.cs` | Player persona data model |
| `WorldProfile.cs` | World data model (contains character + user profile lists) |

**API integration** is in `MainWindow.xaml.cs`:
- Endpoint: `https://api.anthropic.com/v1/messages`
- Model: selectable in-app via `_manager.SelectedModel` (persisted in `worlds.json`); options defined in `AvailableModels` array — default is `claude-haiku-4-5-20251001`
- API key is currently hardcoded as `private const string API_KEY` — move to config before sharing
- `CallClaudeAPI(CharacterProfile profile, string userInput)` — `userInput` is used for lorebook keyword matching before the API call

**System prompt construction** happens in `CharacterProfile.BuildSystemPrompt()` — assembles world context, character attributes, relationships, speech style, and an optional `[로어북]` section from keyword-matched `LoreEntry` items. Lorebook entries are stored per-character in `CharacterProfile.Lore` and filtered in `CallClaudeAPI` before being passed to `BuildSystemPrompt`.

## UI Structure

Two-panel dark-themed layout (`#1a1a2e` / `#7b2ff7` purple palette):
- **Left sidebar:** world list (top) → character list (bottom) within selected world
- **Right panel:** chat history + message input

All UI text and code comments are in Korean.

`App.xaml` defines global implicit styles for `ComboBox` and `ComboBoxItem` (dark background `#16213e`, light text, purple highlight on hover/open). These apply automatically to every `ComboBox` in the app — do not set `Background`/`Foreground` on individual `ComboBoxItem` instances in code-behind, as local values override the style and break hover effects.

## Common Patterns

```csharp
// Access active character
_manager.ActiveWorld?.ActiveCharacter

// Always save after any data change
_manager.Save();

// Refresh UI after character list changes
RefreshCharacterList();
LoadActiveCharacterChat();

// Wrap user input before sending to API
string wrappedInput = $"[현재 상황 서술]\n{userInput}\n\n위 상황에서 {profile.Name}으로서 반응해주세요...";
```

## Critical Rules

- **Never remove `partial`** from window classes — XAML auto-generates a matching partial class
- **Never duplicate `x:Name`** in XAML — causes CS0111 build errors; fix by deleting `obj/` folder and rebuilding
- **Always call `_manager.Save()`** after modifying any data
- **Do not save API error responses** to `ConversationHistory` — check for `"(오류가 발생했습니다"` prefix before saving
- **`CharacterSettingsWindow` constructor takes 4 params:** `(current, otherCharacters, world, manager)`
- **`BuildSystemPrompt()` takes 4 params:** `(world, worldCharacters, userProfile, matchingLore)` — `matchingLore` defaults to `null` (no lore section appended)
- **Input wrapping:** user-visible bubble shows raw input; `ConversationHistory` stores wrapped version — `UnwrapInput()` reverses this on chat reload
- **Deduplication runs on every load** — `WorldManager.Load()` deduplicates worlds, characters, user profiles, chat messages, custom fields (by Label+Value), and lore entries (by Id)
- **Computed properties must have `[JsonIgnore]`** — `ActiveWorld` (WorldManager) and `ActiveCharacter` (WorldProfile) are getter-only properties computed at runtime. Without `[JsonIgnore]`, Newtonsoft.Json serializes them as redundant duplicate objects in `worlds.json`. Any new computed property that derives from existing data must also carry `[JsonIgnore]`.

## Known Issues & Fixes

- **Duplicate data in worlds.json** → root cause was `ActiveWorld` / `ActiveCharacter` computed properties being serialized; fixed by adding `[JsonIgnore]`. Remaining list-level duplicates (characters, messages, custom fields, lore) are cleaned up by deduplication in `WorldManager.Load()`.
- **`obj/` cache conflicts** → delete `obj/` and `bin/` folders, then rebuild
- **`UnwrapInput()` must normalize `\r\n` → `\n`** before parsing wrapped messages