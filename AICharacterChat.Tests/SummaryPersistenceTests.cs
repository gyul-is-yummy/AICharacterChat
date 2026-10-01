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

        [Fact]
        public async Task LoadRepairsDuplicateSummaryIdsWithinSessionAndPersistsRepair()
        {
            var repository = CreateRepository(out var paths);
            var duplicateId = Guid.NewGuid();
            var messages = CreateMessages("pair");
            var first = CreateSummary(duplicateId, messages, "A", revision: 2);
            var second = CreateSummary(duplicateId, messages, "B", revision: 7);
            await WriteStoreAsync(paths, CreateStoreWithSessions(
                CreateSession("session-a", messages, first, second)));

            var loaded = await repository.LoadAsync();

            var summaries = loaded.Worlds.Single().ChatSessions.Single().Summaries;
            Assert.Equal(2, summaries.Count);
            Assert.Equal(duplicateId, summaries[0].Id);
            Assert.NotEqual(duplicateId, summaries[1].Id);
            Assert.NotEqual(Guid.Empty, summaries[1].Id);
            Assert.NotEqual(summaries[0].Id, summaries[1].Id);
            Assert.Equal(["A", "B"], summaries.Select(summary => summary.Title).ToList());
            AssertSummaryDataEqual(second, summaries[1]);

            var persisted = await ReadStoreAsync(paths);
            var persistedSummaries = persisted.Worlds.Single().ChatSessions.Single().Summaries;
            Assert.Equal(summaries[1].Id, persistedSummaries[1].Id);

            var reloaded = await repository.LoadAsync();
            Assert.Equal(summaries[1].Id, reloaded.Worlds.Single().ChatSessions.Single().Summaries[1].Id);
        }

        [Fact]
        public async Task LoadRepairsDuplicateSummaryIdChain()
        {
            var repository = CreateRepository(out var paths);
            var duplicateId = Guid.NewGuid();
            var messages = CreateMessages("chain");
            await WriteStoreAsync(paths, CreateStoreWithSessions(
                CreateSession(
                    "session-a",
                    messages,
                    CreateSummary(duplicateId, messages, "A", revision: 1),
                    CreateSummary(duplicateId, messages, "B", revision: 2),
                    CreateSummary(duplicateId, messages, "C", revision: 3))));

            var loaded = await repository.LoadAsync();

            var summaries = loaded.Worlds.Single().ChatSessions.Single().Summaries;
            Assert.Equal(3, summaries.Count);
            Assert.Equal(duplicateId, summaries[0].Id);
            Assert.Equal(3, summaries.Select(summary => summary.Id).Distinct().Count());
            Assert.All(summaries, summary => Assert.NotEqual(Guid.Empty, summary.Id));
            Assert.Equal(["A", "B", "C"], summaries.Select(summary => summary.Title).ToList());
        }

        [Fact]
        public async Task LoadRepairsEmptySummaryIds()
        {
            var repository = CreateRepository(out var paths);
            var messages = CreateMessages("empty");
            await WriteStoreAsync(paths, CreateStoreWithSessions(
                CreateSession(
                    "session-a",
                    messages,
                    CreateSummary(Guid.Empty, messages, "A", revision: 1),
                    CreateSummary(Guid.Empty, messages, "B", revision: 2))));

            var loaded = await repository.LoadAsync();

            var summaries = loaded.Worlds.Single().ChatSessions.Single().Summaries;
            Assert.Equal(2, summaries.Count);
            Assert.All(summaries, summary => Assert.NotEqual(Guid.Empty, summary.Id));
            Assert.NotEqual(summaries[0].Id, summaries[1].Id);

            var persisted = await ReadStoreAsync(paths);
            Assert.All(persisted.Worlds.Single().ChatSessions.Single().Summaries,
                summary => Assert.NotEqual(Guid.Empty, summary.Id));
        }

        [Fact]
        public async Task LoadLeavesUniqueSummaryIdsUntouched()
        {
            var repository = CreateRepository(out var paths);
            var firstId = Guid.NewGuid();
            var secondId = Guid.NewGuid();
            var messages = CreateMessages("unique");
            await WriteStoreAsync(paths, CreateStoreWithSessions(
                CreateSession(
                    "session-a",
                    messages,
                    CreateSummary(firstId, messages, "A", revision: 1),
                    CreateSummary(secondId, messages, "B", revision: 2))));

            var loaded = await repository.LoadAsync();

            var summaries = loaded.Worlds.Single().ChatSessions.Single().Summaries;
            Assert.Equal(firstId, summaries[0].Id);
            Assert.Equal(secondId, summaries[1].Id);
        }

        [Fact]
        public async Task LoadRepairsSummaryIdsPerSessionOnly()
        {
            var repository = CreateRepository(out var paths);
            var sharedId = Guid.NewGuid();
            var sessionAMessages = CreateMessages("session-a");
            var sessionBMessages = CreateMessages("session-b");
            await WriteStoreAsync(paths, CreateStoreWithSessions(
                CreateSession(
                    "session-a",
                    sessionAMessages,
                    CreateSummary(sharedId, sessionAMessages, "A1", revision: 1),
                    CreateSummary(sharedId, sessionAMessages, "A2", revision: 2)),
                CreateSession(
                    "session-b",
                    sessionBMessages,
                    CreateSummary(sharedId, sessionBMessages, "B1", revision: 3))));

            var loaded = await repository.LoadAsync();

            var sessions = loaded.Worlds.Single().ChatSessions;
            Assert.Equal(sharedId, sessions[0].Summaries[0].Id);
            Assert.NotEqual(sharedId, sessions[0].Summaries[1].Id);
            Assert.Equal(sharedId, sessions[1].Summaries.Single().Id);
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

        private static ChatMessage[] CreateMessages(string prefix) =>
        [
            new ChatMessage(ChatRole.User, $"{prefix}-start"),
            new ChatMessage(ChatRole.Assistant, $"{prefix}-end")
        ];

        private static ConversationSummary CreateSummary(
            Guid id,
            IReadOnlyList<ChatMessage> messages,
            string title,
            int revision)
        {
            var createdAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
                .AddMinutes(revision);
            var updatedAt = createdAt.AddHours(1);

            return new ConversationSummary
            {
                Id = id,
                StartMessageId = messages[0].Id,
                EndMessageId = messages[^1].Id,
                Title = title,
                CurrentSituation = $"{title}-상황",
                KeyEvents = $"{title}-사건",
                RelationshipChanges = $"{title}-관계",
                PromisesAndImportantStatements = $"{title}-약속",
                UnresolvedMatters = $"{title}-미해결",
                PersistentState = $"{title}-상태",
                CreatedAt = createdAt,
                UpdatedAt = updatedAt,
                Revision = revision
            };
        }

        private static ChatSession CreateSession(
            string id,
            IReadOnlyList<ChatMessage> messages,
            params ConversationSummary[] summaries) =>
            new()
            {
                Id = id,
                WorldId = "world-1",
                CharacterId = "char-1",
                UserPersonaId = "user-1",
                Messages = messages.ToList(),
                Summaries = summaries.ToList()
            };

        private static WorldStore CreateStoreWithSessions(params ChatSession[] sessions) =>
            new()
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
                        ChatSessions = sessions.ToList(),
                        ActiveCharacterId = "char-1"
                    }
                ]
            };

        private static async Task WriteStoreAsync(AppDataPaths paths, WorldStore store)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(paths.WorldStorePath)!);
            string json = JsonConvert.SerializeObject(store, Formatting.Indented);
            await File.WriteAllTextAsync(paths.WorldStorePath, json);
        }

        private static async Task<WorldStore> ReadStoreAsync(AppDataPaths paths)
        {
            string json = await File.ReadAllTextAsync(paths.WorldStorePath);
            return JsonConvert.DeserializeObject<WorldStore>(json)!;
        }

        private static void AssertSummaryDataEqual(
            ConversationSummary expected,
            ConversationSummary actual)
        {
            Assert.Equal(expected.StartMessageId, actual.StartMessageId);
            Assert.Equal(expected.EndMessageId, actual.EndMessageId);
            Assert.Equal(expected.Title, actual.Title);
            Assert.Equal(expected.CurrentSituation, actual.CurrentSituation);
            Assert.Equal(expected.KeyEvents, actual.KeyEvents);
            Assert.Equal(expected.RelationshipChanges, actual.RelationshipChanges);
            Assert.Equal(expected.PromisesAndImportantStatements, actual.PromisesAndImportantStatements);
            Assert.Equal(expected.UnresolvedMatters, actual.UnresolvedMatters);
            Assert.Equal(expected.PersistentState, actual.PersistentState);
            Assert.Equal(expected.CreatedAt, actual.CreatedAt);
            Assert.Equal(expected.UpdatedAt, actual.UpdatedAt);
            Assert.Equal(expected.Revision, actual.Revision);
        }
    }
}
