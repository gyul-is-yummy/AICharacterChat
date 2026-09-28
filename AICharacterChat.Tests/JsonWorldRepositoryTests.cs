using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AICharacterChat.Domain.Enums;
using AICharacterChat.Domain.Models;
using AICharacterChat.Infrastructure.Persistence;
using Newtonsoft.Json;
using Xunit;

namespace AICharacterChat.Tests
{
    public class JsonWorldRepositoryTests
    {
        [Fact]
        public async Task SavesAndLoadsWorldStore()
        {
            var repository = CreateRepository(out _);
            var store = CreateStore("저장 테스트", "원문");

            await repository.SaveAsync(store);
            var loaded = await repository.LoadAsync();

            Assert.Equal("저장 테스트", loaded.Worlds[0].Name);
            Assert.Equal("원문", loaded.Worlds[0].ChatSessions[0].Messages[0].Content);
        }

        [Fact]
        public async Task ConcurrentSavesProduceReadableJson()
        {
            var repository = CreateRepository(out _);
            var tasks = Enumerable.Range(0, 20)
                .Select(i => repository.SaveAsync(CreateStore($"세계-{i}", $"메시지-{i}")));

            await Task.WhenAll(tasks);
            var loaded = await repository.LoadAsync();

            Assert.Single(loaded.Worlds);
            Assert.False(string.IsNullOrWhiteSpace(loaded.Worlds[0].Name));
            Assert.False(string.IsNullOrWhiteSpace(loaded.Worlds[0].ChatSessions[0].Messages[0].Content));
        }

        [Fact]
        public async Task FailedSaveDoesNotCorruptExistingFile()
        {
            var repository = CreateRepository(out var paths);
            var original = CreateStore("원본", "원본 메시지");
            await repository.SaveAsync(original);

            await Assert.ThrowsAnyAsync<IOException>(async () =>
            {
                using var blocker = File.Open(paths.WorldStorePath, FileMode.Open, FileAccess.Read, FileShare.None);
                await repository.SaveAsync(CreateStore("새 값", "새 메시지"));
            });

            string json = await File.ReadAllTextAsync(paths.WorldStorePath);
            var loaded = JsonConvert.DeserializeObject<WorldStore>(json)!;
            Assert.Equal("원본", loaded.Worlds[0].Name);
            Assert.Equal("원본 메시지", loaded.Worlds[0].ChatSessions[0].Messages[0].Content);
        }

        private static JsonWorldRepository CreateRepository(out AppDataPaths paths)
        {
            string root = Path.Combine(Path.GetTempPath(), "AICharacterChatTests", Guid.NewGuid().ToString());
            paths = new AppDataPaths(root, Path.Combine(root, "missing-worlds.json"));
            return new JsonWorldRepository(paths, new LegacyDataMigrator());
        }

        private static WorldStore CreateStore(string worldName, string message)
        {
            return new WorldStore
            {
                ActiveWorldId = "world-1",
                Worlds =
                [
                    new World
                    {
                        Id = "world-1",
                        Name = worldName,
                        Characters = [new Character { Id = "char-1", Name = "캐릭터" }],
                        UserPersonas = [new UserPersona { Id = "user-1", Name = "나" }],
                        ChatSessions =
                        [
                            new ChatSession
                            {
                                Id = "session-1",
                                WorldId = "world-1",
                                CharacterId = "char-1",
                                UserPersonaId = "user-1",
                                Messages = [new ChatMessage(ChatRole.User, message)]
                            }
                        ]
                    }
                ]
            };
        }
    }
}
