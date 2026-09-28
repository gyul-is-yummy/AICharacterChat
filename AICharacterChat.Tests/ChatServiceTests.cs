using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AICharacterChat.Application.Chat;
using AICharacterChat.Domain.Enums;
using AICharacterChat.Domain.Models;
using Xunit;

namespace AICharacterChat.Tests
{
    public class ChatServiceTests
    {
        [Fact]
        public async Task SuccessKeepsUserAndAssistantAndSavesOnce()
        {
            var (store, world, character, session) = CreateContext();
            var client = new FakeChatModelClient { Reply = "답변" };
            var repository = new FakeWorldRepository();
            var service = CreateService(client, repository);

            var result = await service.SendAsync(store, world, character, session, "원문 입력", "model");

            Assert.True(result.IsSuccess);
            Assert.Equal(2, session.Messages.Count);
            Assert.Equal(ChatRole.User, session.Messages[0].Role);
            Assert.Equal("원문 입력", session.Messages[0].Content);
            Assert.Equal(ChatRole.Assistant, session.Messages[1].Role);
            Assert.Contains("[현재 상황 서술]", client.LastRequest!.Messages[0].Content);
            Assert.Equal(1, repository.SaveCount);
        }

        [Fact]
        public async Task ApiFailureRollsBackUserAndDoesNotSave()
        {
            var (store, world, character, session) = CreateContextWithExistingMessage();
            var before = session.Messages.ToList();
            var service = CreateService(
                new FakeChatModelClient { Exception = new InvalidOperationException("API 실패") },
                new FakeWorldRepository());

            var result = await service.SendAsync(store, world, character, session, "실패 입력", "model");

            Assert.False(result.IsSuccess);
            Assert.Equal(before, session.Messages);
        }

        [Fact]
        public async Task ApiCancellationRollsBackUserAndDoesNotSave()
        {
            var (store, world, character, session) = CreateContextWithExistingMessage();
            var before = session.Messages.ToList();
            var repository = new FakeWorldRepository();
            var service = CreateService(new FakeChatModelClient { Cancel = true }, repository);

            var result = await service.SendAsync(store, world, character, session, "취소 입력", "model");

            Assert.True(result.IsCanceled);
            Assert.Equal(before, session.Messages);
            Assert.Equal(0, repository.SaveCount);
        }

        [Fact]
        public async Task SaveFailureRollsBackUserAndAssistant()
        {
            var (store, world, character, session) = CreateContextWithExistingMessage();
            var before = session.Messages.ToList();
            var service = CreateService(
                new FakeChatModelClient { Reply = "답변" },
                new FakeWorldRepository { SaveException = new IOException("저장 실패") });

            var result = await service.SendAsync(store, world, character, session, "저장 실패 입력", "model");

            Assert.False(result.IsSuccess);
            Assert.Equal(before, session.Messages);
        }

        [Fact]
        public async Task SaveCancellationRollsBackUserAndAssistant()
        {
            var (store, world, character, session) = CreateContextWithExistingMessage();
            var before = session.Messages.ToList();
            var service = CreateService(
                new FakeChatModelClient { Reply = "답변" },
                new FakeWorldRepository { CancelSave = true });

            var result = await service.SendAsync(store, world, character, session, "저장 취소 입력", "model");

            Assert.True(result.IsCanceled);
            Assert.Equal(before, session.Messages);
        }

        private static ChatService CreateService(FakeChatModelClient client, FakeWorldRepository repository) =>
            new(client, repository, new PromptBuilder(), new LoreMatcher());

        private static (WorldStore Store, World World, Character Character, ChatSession Session) CreateContext()
        {
            var world = TestData.CreateWorld();
            var store = new WorldStore { ActiveWorldId = world.Id, Worlds = [world] };
            return (store, world, world.Characters[0], world.ChatSessions[0]);
        }

        private static (WorldStore Store, World World, Character Character, ChatSession Session) CreateContextWithExistingMessage()
        {
            var context = CreateContext();
            context.Session.Messages.Add(new ChatMessage(ChatRole.Assistant, "기존 메시지"));
            return context;
        }
    }
}
