using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AICharacterChat.Application.Interfaces;
using AICharacterChat.Domain.Models;
using Newtonsoft.Json;

namespace AICharacterChat.Infrastructure.Persistence
{
    public class JsonWorldRepository : IWorldRepository
    {
        private readonly AppDataPaths _paths;
        private readonly LegacyDataMigrator _legacyDataMigrator;

        public JsonWorldRepository(AppDataPaths paths, LegacyDataMigrator legacyDataMigrator)
        {
            _paths = paths;
            _legacyDataMigrator = legacyDataMigrator;
        }

        public async Task<WorldStore> LoadAsync(CancellationToken cancellationToken = default)
        {
            _paths.EnsureDirectories();

            if (File.Exists(_paths.WorldStorePath))
            {
                string json = await File.ReadAllTextAsync(_paths.WorldStorePath, cancellationToken);
                var store = JsonConvert.DeserializeObject<WorldStore>(json) ?? CreateDefaultStore();
                EnsureRuntimeDefaults(store);
                return store;
            }

            if (File.Exists(_paths.LegacyWorldsPath))
            {
                var migrated = await _legacyDataMigrator.MigrateFileAsync(
                    _paths.LegacyWorldsPath,
                    cancellationToken);
                await SaveAsync(migrated, cancellationToken);
                return migrated;
            }

            var defaultStore = CreateDefaultStore();
            await SaveAsync(defaultStore, cancellationToken);
            return defaultStore;
        }

        public async Task SaveAsync(WorldStore store, CancellationToken cancellationToken = default)
        {
            _paths.EnsureDirectories();
            string json = JsonConvert.SerializeObject(store, Formatting.Indented);
            await JsonFileWriter.WriteAtomicallyAsync(_paths.WorldStorePath, json, cancellationToken);
        }

        private static WorldStore CreateDefaultStore()
        {
            var world = new World
            {
                Name = "로맨스 판타지",
                Genre = "로맨스 판타지",
                Era = "현대",
                Description = "기본 세계관입니다."
            };

            var defaultUser = new UserPersona { Name = "나" };
            world.UserPersonas.Add(defaultUser);

            var character = new Character
            {
                Name = "이준혁",
                Age = "28세",
                Job = "재벌 3세",
                Appearance = "차갑고 날카로운 눈매, 짧은 검은 머리",
                Personality = "겉으로는 냉정하고 무뚝뚝하지만 내면은 따뜻함",
                SpeechStyle = "반드시 반말 사용\n감정을 잘 드러내지 않음\n가끔 비꼬는 듯한 말투",
                DefaultScenario = "사용자와 계약결혼 관계\n사용자를 내심 신경 쓰지만 절대 티 내지 않음"
            };
            world.Characters.Add(character);
            world.ActiveCharacterId = character.Id;
            world.ChatSessions.Add(new ChatSession
            {
                WorldId = world.Id,
                CharacterId = character.Id,
                UserPersonaId = defaultUser.Id
            });

            return new WorldStore
            {
                Worlds = [world],
                ActiveWorldId = world.Id
            };
        }

        private static void EnsureRuntimeDefaults(WorldStore store)
        {
            if (store.Worlds.Count == 0)
            {
                var defaultStore = CreateDefaultStore();
                store.Worlds = defaultStore.Worlds;
                store.ActiveWorldId = defaultStore.ActiveWorldId;
                return;
            }

            if (string.IsNullOrWhiteSpace(store.ActiveWorldId))
                store.ActiveWorldId = store.Worlds[0].Id;

            foreach (var world in store.Worlds)
            {
                if (world.UserPersonas.Count == 0)
                    world.UserPersonas.Add(new UserPersona { Name = "나" });

                if (world.Characters.Count == 0)
                    continue;

                if (string.IsNullOrWhiteSpace(world.ActiveCharacterId))
                    world.ActiveCharacterId = world.Characters[0].Id;

                foreach (var character in world.Characters)
                {
                    if (world.GetSessionForCharacter(character.Id) == null)
                    {
                        world.ChatSessions.Add(new ChatSession
                        {
                            WorldId = world.Id,
                            CharacterId = character.Id,
                            UserPersonaId = world.UserPersonas[0].Id
                        });
                    }
                }
            }
        }
    }
}
