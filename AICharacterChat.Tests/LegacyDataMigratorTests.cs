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
    public class LegacyDataMigratorTests
    {
        [Fact]
        public void MigratesLegacyDataWithoutLoss()
        {
            var store = new LegacyDataMigrator().MigrateJson(TestData.CreateLegacyJson());
            var world = store.Worlds.Single();
            var character = world.Characters.Single();
            var session = world.ChatSessions.Single();

            Assert.Equal("world-1", world.Id);
            Assert.Equal("세계", world.Name);
            Assert.Equal("char-1", character.Id);
            Assert.Equal("캐릭터", character.Name);
            Assert.Equal("user-1", world.UserPersonas.Single().Id);
            Assert.Equal("관계", character.Relationships.Single().Description);
            Assert.Equal("취향", character.CustomFields.Single().Label);
            Assert.Equal("로어", character.Lore.Single().Title);
            Assert.Equal("기본 상황", character.DefaultScenario);
            Assert.Equal("", session.Scenario);
            Assert.Equal("user-1", session.UserPersonaId);
            Assert.Equal(["원래 입력", "응답", "반복 입력", "반복 입력"], session.Messages.Select(m => m.Content).ToArray());
            Assert.Equal([ChatRole.User, ChatRole.Assistant, ChatRole.User, ChatRole.User], session.Messages.Select(m => m.Role).ToArray());
        }

        [Fact]
        public void ReMigratingLegacyJsonDoesNotCreateDuplicateSessions()
        {
            var first = new LegacyDataMigrator().MigrateJson(TestData.CreateLegacyJson());
            var second = new LegacyDataMigrator().MigrateJson(TestData.CreateLegacyJson());

            Assert.Single(first.Worlds.Single().ChatSessions);
            Assert.Single(second.Worlds.Single().ChatSessions);
        }

        [Fact]
        public void AlreadyMigratedJsonIsNotConvertedAsLegacy()
        {
            var migrated = new LegacyDataMigrator().MigrateJson(TestData.CreateLegacyJson());
            string json = JsonConvert.SerializeObject(migrated);

            var loaded = new LegacyDataMigrator().MigrateJson(json);

            Assert.Single(loaded.Worlds.Single().ChatSessions);
            Assert.Equal(4, loaded.Worlds.Single().ChatSessions.Single().Messages.Count);
        }

        [Fact]
        public async Task CreatesBackupAndDoesNotOverwriteExistingBackup()
        {
            string dir = Path.Combine(Path.GetTempPath(), "AICharacterChatTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(dir);
            string legacyPath = Path.Combine(dir, "worlds.json");
            string backupPath = legacyPath + ".bak";
            await File.WriteAllTextAsync(legacyPath, TestData.CreateLegacyJson());
            await File.WriteAllTextAsync(backupPath, "existing backup");

            await new LegacyDataMigrator().MigrateFileAsync(legacyPath);

            Assert.Equal("existing backup", await File.ReadAllTextAsync(backupPath));
        }

        [Fact]
        public async Task InvalidMigrationKeepsLegacyOriginalFile()
        {
            string dir = Path.Combine(Path.GetTempPath(), "AICharacterChatTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(dir);
            string legacyPath = Path.Combine(dir, "worlds.json");
            await File.WriteAllTextAsync(legacyPath, "{ invalid json");

            await Assert.ThrowsAsync<JsonReaderException>(() => new LegacyDataMigrator().MigrateFileAsync(legacyPath));

            Assert.Equal("{ invalid json", await File.ReadAllTextAsync(legacyPath));
            Assert.True(File.Exists(legacyPath + ".bak"));
        }

        [Fact]
        public async Task SettingsRepositoryMigratesLegacySelectedModel()
        {
            string dir = Path.Combine(Path.GetTempPath(), "AICharacterChatTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(dir);
            string legacyPath = Path.Combine(dir, "worlds.json");
            await File.WriteAllTextAsync(legacyPath, TestData.CreateLegacyJson());
            var settings = await new JsonSettingsRepository(new AppDataPaths(Path.Combine(dir, "appdata"), legacyPath)).LoadAsync();

            Assert.Equal("legacy-model", settings.SelectedModel);
        }
    }
}
