# Migration

Legacy data is read from the old executable-adjacent `worlds.json` when the new `%LocalAppData%/AICharacterChat/data/worlds.json` file does not exist.

Before conversion, `LegacyDataMigrator` creates:

```text
worlds.json.bak
```

if a backup does not already exist.

## Conversion Rules

```text
WorldProfile -> World
CharacterProfile -> Character
UserProfile -> UserPersona
CharacterProfile.ConversationHistory -> default ChatSession.Messages
CharacterProfile.SelectedUserProfileId -> default ChatSession.UserPersonaId
CharacterProfile.Situation -> Character.DefaultScenario
```

Each migrated character gets one default `ChatSession`. The app does not add UI for multiple sessions in this refactor.

Migrated `ChatSession.Scenario` is intentionally empty so edits to `Character.DefaultScenario` continue to affect the current chat unless a future session-specific scenario is explicitly set.

## Message Handling

Legacy user messages saved in the old wrapped form:

```text
[현재 상황 서술]
...

위 상황에서 ...
```

are unwrapped so only the original user input is stored in `ChatMessage.Content`.

Assistant messages are copied unchanged.

The new loader does not remove duplicate-looking messages. Repeated dialogue can be intentional and must be preserved.

## Settings

`SelectedModel` moves from legacy `worlds.json` to:

```text
%LocalAppData%/AICharacterChat/settings.json
```

If `settings.json` does not exist, `JsonSettingsRepository` attempts to read the legacy selected model once.

## Safety

Migration creates a backup before parsing the legacy file and does not overwrite an existing `.bak` file.

If JSON parsing fails, the legacy source file is left untouched. The repository only runs legacy migration when the new `%LocalAppData%/AICharacterChat/data/worlds.json` file is absent.

If a file already contains the new `WorldStore` shape, `LegacyDataMigrator` returns it as current data instead of applying legacy conversion again.
