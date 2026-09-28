using System;
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
    public class SummaryPersistenceTests
    {
        [Fact]
        public async Task OldJsonWithoutSummariesLoadsWithEmptyList()
        {
            string root = Path.Combine(Path.GetTempPath(), "AICharacterChatTests", Guid.NewGuid().ToString());
            var paths = new AppDataPaths(root, Path.Combine(root, "missing-worlds.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(paths.WorldStorePath)!);
            await File.WriteAllTextAsync(paths.WorldStorePath, CreateJsonWithoutSummaries());
            var repository = new JsonWorldRepository(paths, new LegacyDataMigrator());

            var store = await repository.LoadAsync();

            Assert.NotNull(store.Worlds[0].ChatSessions[0].Summaries);
            Assert.Empty(store.Worlds[0].ChatSessions[0].Summaries);
        }

        [Fact]
        public async Task SummaryRoundTripPreservesAllFields()
        {
            var repository = CreateRepository(out _);
            var message1 = new ChatMessage(ChatRole.User, "시작");
            var message2 = new ChatMessage(ChatRole.Assistant, "끝");
            var createdAt = DateTimeOffset.Now.AddDays(-1);
            var updatedAt = DateTimeOffset.Now;
            var summary = new ConversationSummary
            {
                Id = Guid.NewGuid(),
                StartMessageId = message1.Id,
                EndMessageId = message2.Id,
                Title = "첫 만남",
                CurrentSituation = "상황",
                KeyEvents = "사건",
                RelationshipChanges = "관계",
                PromisesAndImportantStatements = "약속",
                UnresolvedMatters = "미해결",
                PersistentState = "상태",
                CreatedAt = createdAt,
                UpdatedAt = updatedAt,
                Revision = 3
            };
            var store = CreateStore(message1, message2, summary);

            await repository.SaveAsync(store);
            var loaded = await repository.LoadAsync();
            var loadedSummary = loaded.Worlds[0].ChatSessions[0].Summaries.Single();

            Assert.Equal(summary.Id, loadedSummary.Id);
            Assert.Equal(message1.Id, loadedSummary.StartMessageId);
            Assert.Equal(message2.Id, loadedSummary.EndMessageId);
            Assert.Equal("첫 만남", loadedSummary.Title);
            Assert.Equal("상황", loadedSummary.CurrentSituation);
            Assert.Equal("사건", loadedSummary.KeyEvents);
            Assert.Equal("관계", loadedSummary.RelationshipChanges);
            Assert.Equal("약속", loadedSummary.PromisesAndImportantStatements);
            Assert.Equal("미해결", loadedSummary.UnresolvedMatters);
            Assert.Equal("상태", loadedSummary.PersistentState);
            Assert.Equal(3, loadedSummary.Revision);
        }

        [Fact]
        public void LegacyMigrationCreatesSessionWithEmptySummaries()
        {
            var store = new LegacyDataMigrator().MigrateJson(TestData.CreateLegacyJson());

            Assert.NotNull(store.Worlds.Single().ChatSessions.Single().Summaries);
            Assert.Empty(store.Worlds.Single().ChatSessions.Single().Summaries);
        }

        private static JsonWorldRepository CreateRepository(out AppDataPaths paths)
        {
            string root = Path.Combine(Path.GetTempPath(), "AICharacterChatTests", Guid.NewGuid().ToString());
            paths = new AppDataPaths(root, Path.Combine(root, "missing-worlds.json"));
            return new JsonWorldRepository(paths, new LegacyDataMigrator());
        }

        private static WorldStore CreateStore(ChatMessage message1, ChatMessage message2, ConversationSummary summary)
        {
            return new WorldStore
            {
                ActiveWorldId = "world-1",
                Worlds =
                [
                    new World
                    {
                        Id = "world-1",
                        Name = "세계",
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
                                Messages = [message1, message2],
                                Summaries = [summary]
                            }
                        ]
                    }
                ]
            };
        }

        private static string CreateJsonWithoutSummaries() => """
            {
              "Worlds": [
                {
                  "Id": "world-1",
                  "Name": "세계",
                  "Characters": [{ "Id": "char-1", "Name": "캐릭터" }],
                  "UserPersonas": [{ "Id": "user-1", "Name": "나" }],
                  "ChatSessions": [
                    {
                      "Id": "session-1",
                      "WorldId": "world-1",
                      "CharacterId": "char-1",
                      "UserPersonaId": "user-1",
                      "Messages": [{ "Role": 0, "Content": "원문" }]
                    }
                  ],
                  "ActiveCharacterId": "char-1"
                }
              ],
              "ActiveWorldId": "world-1"
            }
            """;
    }
}
