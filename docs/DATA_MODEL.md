# Data Model

## Store

```text
WorldStore
  ActiveWorldId
  Worlds[]
```

## World

```text
World
  Id
  Name
  Genre
  Era
  Description
  Rules
  Characters[]
  ActiveCharacterId
  UserPersonas[]
  ChatSessions[]
```

`World` owns `ChatSessions` in this refactor. This keeps persistence simple while preserving identifiers for a future split into separate chat storage.

When a new character is created, the selected user persona is passed as creation workflow state and stored in the default `ChatSession.UserPersonaId`. The `Character` model must not regain selected-user/session state.

## Character

`Character` contains only character configuration:

```text
Id
Name
Age
Gender
Job
Appearance
Personality
Etc
Secret
SpeechStyle
DefaultScenario
CustomFields[]
Relationships[]
Lore[]
```

`DefaultScenario` replaces legacy `Situation`.

## ChatSession

```text
Id
WorldId
CharacterId
UserPersonaId
Scenario
Messages[]
```

Scenario selection priority:

```text
if ChatSession.Scenario is not empty -> use ChatSession.Scenario
else -> use Character.DefaultScenario
```

## ChatMessage

```text
Guid Id
ChatRole Role
string Content
DateTimeOffset CreatedAt
```

User `Content` is raw input. Anthropic formatting such as `[현재 상황 서술]` is generated only when creating the provider request.

If a send operation fails or is canceled, the message list is restored to its pre-send state.
