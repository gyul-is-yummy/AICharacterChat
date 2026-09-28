using System.Threading.Tasks;
using AICharacterChat.Application.Chat;
using AICharacterChat.Application.Interfaces;
using AICharacterChat.Application.Models;
using AICharacterChat.Domain.Enums;
using AICharacterChat.Domain.Models;
using AICharacterChat.Presentation.ViewModels;
using Xunit;

namespace AICharacterChat.Tests
{
    public class MainViewModelTests
    {
        [Fact]
        public async Task ClearChatCommandClearsTargetCharacterSessionOnly()
        {
            var world = TestData.CreateWorld();
            var other = new Character { Id = "char-2", Name = "다른 캐릭터" };
            world.Characters.Add(other);
            var otherSession = new ChatSession
            {
                Id = "session-2",
                WorldId = world.Id,
                CharacterId = other.Id,
                UserPersonaId = world.UserPersonas[0].Id,
                Messages = [new ChatMessage(ChatRole.User, "지우면 안 됨")]
            };
            world.ChatSessions[0].Messages.Add(new ChatMessage(ChatRole.User, "지울 메시지"));
            world.ChatSessions.Add(otherSession);

            var store = new WorldStore { ActiveWorldId = world.Id, Worlds = [world] };
            var vm = CreateViewModel(store);
            await vm.InitializeAsync();
            await vm.ClearChatForCharacterAsync(world.Characters[0]);

            Assert.Empty(world.ChatSessions[0].Messages);
            Assert.Single(otherSession.Messages);
        }

        [Fact]
        public async Task AddCharacterUsesSelectedUserPersonaForDefaultSession()
        {
            var world = TestData.CreateWorld();
            world.UserPersonas.Add(new UserPersona { Id = "user-2", Name = "두 번째" });
            var store = new WorldStore { ActiveWorldId = world.Id, Worlds = [world] };
            var vm = CreateViewModel(store);
            await vm.InitializeAsync();
            var character = new Character { Id = "char-new", Name = "새 캐릭터" };

            await vm.AddCharacterAsync(character, "user-2");

            Assert.Equal("user-2", world.GetSessionForCharacter("char-new")!.UserPersonaId);
        }

        private static MainViewModel CreateViewModel(WorldStore store)
        {
            var repository = new MemoryWorldRepository(store);
            return new MainViewModel(
                repository,
                new MemorySettingsRepository(),
                new MemoryModelCatalog(),
                new ChatService(new FakeChatModelClient { Reply = "답" }, repository, new PromptBuilder(), new LoreMatcher()));
        }

        private class MemoryWorldRepository : IWorldRepository
        {
            private readonly WorldStore _store;

            public MemoryWorldRepository(WorldStore store)
            {
                _store = store;
            }

            public Task<WorldStore> LoadAsync(System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(_store);
            public Task SaveAsync(WorldStore store, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        private class MemorySettingsRepository : ISettingsRepository
        {
            public Task<AppSettings> LoadAsync(System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(new AppSettings());
            public Task SaveAsync(AppSettings settings, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        private class MemoryModelCatalog : IChatModelCatalog
        {
            public System.Collections.Generic.IReadOnlyList<ChatModelOption> Models { get; } =
            [
                new ChatModelOption { Id = "model", Label = "Model" }
            ];
        }
    }
}
