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
Summaries[]
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

## ConversationSummary

```text
Guid Id
Guid StartMessageId
Guid EndMessageId
string Title
string CurrentSituation
string KeyEvents
string RelationshipChanges
string PromisesAndImportantStatements
string UnresolvedMatters
string PersistentState
DateTimeOffset CreatedAt
DateTimeOffset UpdatedAt
int Revision
```

`ConversationSummary` represents a user-selected continuous message range in one `ChatSession`.

The range is inclusive and uses message ids instead of persisted indexes. Summary ranges cannot overlap with other summaries in the same session. Adjacent ranges and gaps are allowed.

The six section fields are stored separately. Empty sections use `없음`.

Summaries never remove or mutate source `Messages`. Historical Context is rendered on demand from `Messages`, `Summaries`, and the recent selection; it is not persisted.
