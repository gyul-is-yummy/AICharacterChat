using System;
using System.Linq;
using System.Threading.Tasks;
using AICharacterChat.Application.Chat;
using AICharacterChat.Application.Context;
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
            var targetSummary = new ConversationSummary
            {
                StartMessageId = world.ChatSessions[0].Messages.FirstOrDefault()?.Id ?? Guid.NewGuid(),
                EndMessageId = world.ChatSessions[0].Messages.FirstOrDefault()?.Id ?? Guid.NewGuid(),
                Title = "지울 요약"
            };
            var otherSession = new ChatSession
            {
                Id = "session-2",
                WorldId = world.Id,
                CharacterId = other.Id,
                UserPersonaId = world.UserPersonas[0].Id,
                Messages = [new ChatMessage(ChatRole.User, "지우면 안 됨")],
                Summaries =
                [
                    new ConversationSummary
                    {
                        StartMessageId = Guid.NewGuid(),
                        EndMessageId = Guid.NewGuid(),
                        Title = "다른 요약"
                    }
                ]
            };
            world.ChatSessions[0].Messages.Add(new ChatMessage(ChatRole.User, "지울 메시지"));
            world.ChatSessions[0].Summaries.Add(targetSummary);
            world.ChatSessions.Add(otherSession);

            var store = new WorldStore { ActiveWorldId = world.Id, Worlds = [world] };
            var vm = CreateViewModel(store);
            await vm.InitializeAsync();
            await vm.ClearChatForCharacterAsync(world.Characters[0]);

            Assert.Empty(world.ChatSessions[0].Messages);
            Assert.Empty(world.ChatSessions[0].Summaries);
            Assert.Single(otherSession.Messages);
            Assert.Single(otherSession.Summaries);
        }

        [Fact]
        public async Task ClearChatSaveFailureRollsBackMessagesAndSummaries()
        {
            var world = TestData.CreateWorld();
            var message = new ChatMessage(ChatRole.User, "보존");
            world.ChatSessions[0].Messages.Add(message);
            var summary = new ConversationSummary
            {
                StartMessageId = message.Id,
                EndMessageId = message.Id,
                Title = "보존 요약"
            };
            world.ChatSessions[0].Summaries.Add(summary);
            var store = new WorldStore { ActiveWorldId = world.Id, Worlds = [world] };
            var repository = new MemoryWorldRepository(store);
            var vm = CreateViewModel(repository);
            await vm.InitializeAsync();
            repository.SaveException = new InvalidOperationException("저장 실패");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => vm.ClearChatForCharacterAsync(world.Characters[0]));

            Assert.Contains(message, world.ChatSessions[0].Messages);
            Assert.Contains(summary, world.ChatSessions[0].Summaries);
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
            return CreateViewModel(repository);
        }

        private static MainViewModel CreateViewModel(MemoryWorldRepository repository)
        {
            return new MainViewModel(
                repository,
                new MemorySettingsRepository(),
                new MemoryModelCatalog(),
                new ChatService(
                    new FakeChatModelClient { Reply = "답" },
                    repository,
                    new ContextBuilder(
                        new PromptBuilder(),
                        new LoreMatcher(),
                        new RecentMessageSelector(),
                        new HistoricalContextBuilder())));
        }

        private class MemoryWorldRepository : IWorldRepository
        {
            private readonly WorldStore _store;
            public Exception? SaveException { get; set; }

            public MemoryWorldRepository(WorldStore store)
            {
                _store = store;
            }

            public Task<WorldStore> LoadAsync(System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(_store);
            public Task SaveAsync(WorldStore store, System.Threading.CancellationToken cancellationToken = default)
            {
                if (SaveException != null)
                    throw SaveException;

                return Task.CompletedTask;
            }
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
