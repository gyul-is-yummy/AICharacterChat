using System.Linq;
using AICharacterChat.Application.Chat;
using AICharacterChat.Application.Context;
using AICharacterChat.Domain.Enums;
using AICharacterChat.Domain.Models;
using Xunit;

namespace AICharacterChat.Tests
{
    public class ContextBuilderTests
    {
        [Fact]
        public void UsesPromptBuilderResultForSystemPrompt()
        {
            var world = TestData.CreateWorld();
            var context = CreateBuilder().Build(world, world.Characters[0], world.ChatSessions[0], "입력");

            Assert.Contains("당신은 '캐릭터'입니다.", context.SystemPrompt);
            Assert.Contains("기본 상황", context.SystemPrompt);
        }

        [Fact]
        public void SelectsLoreUsingCurrentRawUserInput()
        {
            var world = TestData.CreateWorld();

            var context = CreateBuilder().Build(world, world.Characters[0], world.ChatSessions[0], "키워드가 있는 입력");

            Assert.Contains("[로어북 - 현재 대화에 적용되는 설정]", context.SystemPrompt);
            Assert.Contains("로어 내용", context.SystemPrompt);
        }

        [Fact]
        public void IncludesOnlyMessagesSelectedByRecentMessageSelector()
        {
            var world = TestData.CreateWorld();
            var session = world.ChatSessions[0];
            for (int i = 1; i <= 30; i++)
                session.Messages.Add(new ChatMessage(ChatRole.User, i.ToString()));

            var context = CreateBuilder(maxRecentMessages: 20).Build(world, world.Characters[0], session, "현재");

            Assert.Equal(20, context.Messages.Count);
            Assert.Contains("11", context.Messages[0].Content);
            Assert.Contains("30", context.Messages[^1].Content);
        }

        [Fact]
        public void WrapsUserMessagesForRequestOnly()
        {
            var world = TestData.CreateWorld();
            var session = world.ChatSessions[0];
            session.Messages.Add(new ChatMessage(ChatRole.User, "오늘 피곤해."));

            var context = CreateBuilder().Build(world, world.Characters[0], session, "오늘 피곤해.");

            Assert.Contains("[현재 상황 서술]", context.Messages[0].Content);
            Assert.Contains("오늘 피곤해.", context.Messages[0].Content);
            Assert.Equal("오늘 피곤해.", session.Messages[0].Content);
        }

        [Fact]
        public void KeepsAssistantContentUnchanged()
        {
            var world = TestData.CreateWorld();
            var session = world.ChatSessions[0];
            session.Messages.Add(new ChatMessage(ChatRole.Assistant, "그대로"));

            var context = CreateBuilder().Build(world, world.Characters[0], session, "입력");

            Assert.Equal("그대로", context.Messages[0].Content);
        }

        [Fact]
        public void DoesNotMutateDomainMessages()
        {
            var world = TestData.CreateWorld();
            var session = world.ChatSessions[0];
            session.Messages.Add(new ChatMessage(ChatRole.User, "원문"));
            session.Messages.Add(new ChatMessage(ChatRole.Assistant, "응답"));
            var original = session.Messages.Select(m => new { m.Role, m.Content }).ToList();

            _ = CreateBuilder().Build(world, world.Characters[0], session, "원문");

            Assert.Equal(original.Select(m => m.Role), session.Messages.Select(m => m.Role));
            Assert.Equal(original.Select(m => m.Content), session.Messages.Select(m => m.Content));
        }

        [Fact]
        public void LimitsContextWhenHistoryHasMoreThanThirtyMessages()
        {
            var world = TestData.CreateWorld();
            var session = world.ChatSessions[0];
            for (int i = 1; i <= 35; i++)
                session.Messages.Add(new ChatMessage(ChatRole.User, i.ToString()));

            var context = CreateBuilder(maxRecentMessages: 20).Build(world, world.Characters[0], session, "35");

            Assert.Equal(20, context.Messages.Count);
            Assert.Contains("16", context.Messages[0].Content);
            Assert.Contains("35", context.Messages[^1].Content);
        }

        [Fact]
        public void KeepsScenarioFallbackBehavior()
        {
            var world = TestData.CreateWorld();
            var session = world.ChatSessions[0];

            var defaultContext = CreateBuilder().Build(world, world.Characters[0], session, "입력");
            session.Scenario = "세션 상황";
            var sessionContext = CreateBuilder().Build(world, world.Characters[0], session, "입력");

            Assert.Contains("기본 상황", defaultContext.SystemPrompt);
            Assert.Contains("세션 상황", sessionContext.SystemPrompt);
            Assert.DoesNotContain("기본 상황", sessionContext.SystemPrompt);
        }

        private static ContextBuilder CreateBuilder(int maxRecentMessages = 20) =>
            new(new PromptBuilder(), new LoreMatcher(), new RecentMessageSelector(maxRecentMessages));
    }
}
