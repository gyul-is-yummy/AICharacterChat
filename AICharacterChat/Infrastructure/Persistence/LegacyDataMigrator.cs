using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AICharacterChat.Domain.Enums;
using AICharacterChat.Domain.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AICharacterChat.Infrastructure.Persistence
{
    public class LegacyDataMigrator
    {
        public async Task<WorldStore> MigrateFileAsync(
            string legacyWorldsPath,
            CancellationToken cancellationToken = default)
        {
            string json = await File.ReadAllTextAsync(legacyWorldsPath, cancellationToken);
            string backupPath = legacyWorldsPath + ".bak";
            if (!File.Exists(backupPath))
                File.Copy(legacyWorldsPath, backupPath);

            return MigrateJson(json);
        }

        public WorldStore MigrateJson(string json)
        {
            if (IsCurrentFormat(json))
                return JsonConvert.DeserializeObject<WorldStore>(json) ?? new WorldStore();

            var legacy = JsonConvert.DeserializeObject<LegacyWorldManager>(json)
                         ?? new LegacyWorldManager();

            var store = new WorldStore
            {
                ActiveWorldId = legacy.ActiveWorldId
            };

            foreach (var legacyWorld in legacy.Worlds)
            {
                var world = new World
                {
                    Id = legacyWorld.Id,
                    Name = legacyWorld.Name,
                    Genre = legacyWorld.Genre,
                    Era = legacyWorld.Era,
                    Description = legacyWorld.Description,
                    Rules = legacyWorld.Rules,
                    ActiveCharacterId = legacyWorld.ActiveCharacterId
                };

                world.UserPersonas = legacyWorld.UserProfiles
                    .Select(u => new UserPersona
                    {
                        Id = u.Id,
                        Name = u.Name,
                        Appearance = u.Appearance,
                        Personality = u.Personality,
                        AdditionalInfo = u.AdditionalInfo
                    })
                    .ToList();

                if (world.UserPersonas.Count == 0)
                    world.UserPersonas.Add(new UserPersona { Name = "나" });

                foreach (var legacyCharacter in legacyWorld.Characters)
                {
                    var character = new Character
                    {
                        Id = legacyCharacter.Id,
                        Name = legacyCharacter.Name,
                        Age = legacyCharacter.Age,
                        Gender = legacyCharacter.Gender,
                        Job = legacyCharacter.Job,
                        Appearance = legacyCharacter.Appearance,
                        Personality = legacyCharacter.Personality,
                        Etc = legacyCharacter.Etc,
                        Secret = legacyCharacter.Secret,
                        SpeechStyle = legacyCharacter.SpeechStyle,
                        DefaultScenario = legacyCharacter.Situation,
                        CustomFields = legacyCharacter.CustomFields,
                        Relationships = legacyCharacter.Relationships,
                        Lore = legacyCharacter.Lore
                    };
                    world.Characters.Add(character);

                    string userPersonaId = string.IsNullOrWhiteSpace(legacyCharacter.SelectedUserProfileId)
                        ? world.UserPersonas[0].Id
                        : legacyCharacter.SelectedUserProfileId;

                    world.ChatSessions.Add(new ChatSession
                    {
                        WorldId = world.Id,
                        CharacterId = character.Id,
                        UserPersonaId = userPersonaId,
                        Scenario = "",
                        Messages = legacyCharacter.ConversationHistory
                            .Select(MigrateMessage)
                            .ToList()
                    });
                }

                if (string.IsNullOrWhiteSpace(world.ActiveCharacterId) && world.Characters.Count > 0)
                    world.ActiveCharacterId = world.Characters[0].Id;

                store.Worlds.Add(world);
            }

            if (string.IsNullOrWhiteSpace(store.ActiveWorldId) && store.Worlds.Count > 0)
                store.ActiveWorldId = store.Worlds[0].Id;

            return store;
        }

        private static bool IsCurrentFormat(string json)
        {
            try
            {
                var root = JObject.Parse(json);
                var firstWorld = root["Worlds"]?.FirstOrDefault();
                return firstWorld?["UserPersonas"] != null ||
                       firstWorld?["ChatSessions"] != null;
            }
            catch
            {
                return false;
            }
        }

        public static string UnwrapLegacyInput(string wrapped)
        {
            wrapped = wrapped.Replace("\r\n", "\n");
            const string header = "[현재 상황 서술]\n";
            int start = wrapped.IndexOf(header, StringComparison.Ordinal);
            if (start < 0)
                return wrapped;

            start += header.Length;
            int end = wrapped.IndexOf("\n\n위 상황에서", StringComparison.Ordinal);
            return end < 0 ? wrapped[start..].Trim() : wrapped[start..end].Trim();
        }

        private static AICharacterChat.Domain.Models.ChatMessage MigrateMessage(LegacyChatMessage legacyMessage)
        {
            var role = string.Equals(legacyMessage.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                ? ChatRole.Assistant
                : ChatRole.User;

            string content = role == ChatRole.User
                ? UnwrapLegacyInput(legacyMessage.Content)
                : legacyMessage.Content;

            return new AICharacterChat.Domain.Models.ChatMessage(role, content);
        }

        private class LegacyWorldManager
        {
            public List<LegacyWorld> Worlds { get; set; } = new();
            public string ActiveWorldId { get; set; } = "";
        }

        private class LegacyWorld
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string Name { get; set; } = "새 세계관";
            public string Genre { get; set; } = "";
            public string Era { get; set; } = "";
            public string Description { get; set; } = "";
            public string Rules { get; set; } = "";
            public List<LegacyCharacter> Characters { get; set; } = new();
            public string ActiveCharacterId { get; set; } = "";
            public List<LegacyUserProfile> UserProfiles { get; set; } = new();
        }

        private class LegacyCharacter
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string Name { get; set; } = "새 캐릭터";
            public string Age { get; set; } = "";
            public string Gender { get; set; } = "";
            public string Job { get; set; } = "";
            public string Appearance { get; set; } = "";
            public string Personality { get; set; } = "";
            public string Etc { get; set; } = "";
            public string Secret { get; set; } = "";
            public string SpeechStyle { get; set; } = "";
            public string Situation { get; set; } = "";
            public List<AICharacterChat.Domain.Models.CustomField> CustomFields { get; set; } = new();
            public string SelectedUserProfileId { get; set; } = "";
            public List<AICharacterChat.Domain.Models.CharacterRelationship> Relationships { get; set; } = new();
            public List<LegacyChatMessage> ConversationHistory { get; set; } = new();
            public List<AICharacterChat.Domain.Models.LoreEntry> Lore { get; set; } = new();
        }

        private class LegacyChatMessage
        {
            public string Role { get; set; } = "";
            public string Content { get; set; } = "";
        }

        private class LegacyUserProfile
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string Name { get; set; } = "나";
            public string Appearance { get; set; } = "";
            public string Personality { get; set; } = "";
            public string AdditionalInfo { get; set; } = "";
        }
    }
}
