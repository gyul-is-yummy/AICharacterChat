using AICharacterChat.Application.Chat;
using Xunit;

namespace AICharacterChat.Tests
{
    public class PromptBuilderTests
    {
        [Fact]
        public void UsesDefaultScenarioWhenSessionScenarioIsEmpty()
        {
            var world = TestData.CreateWorld();
            string prompt = new PromptBuilder().Build(world, world.Characters[0], world.UserPersonas[0], world.ChatSessions[0], []);

            Assert.Contains("기본 상황", prompt);
        }

        [Fact]
        public void UsesSessionScenarioWhenPresent()
        {
            var world = TestData.CreateWorld();
            world.ChatSessions[0].Scenario = "세션 전용 상황";

            string prompt = new PromptBuilder().Build(world, world.Characters[0], world.UserPersonas[0], world.ChatSessions[0], []);

            Assert.Contains("세션 전용 상황", prompt);
            Assert.DoesNotContain("기본 상황", prompt);
        }

        [Fact]
        public void IncludesWorldCharacterUserRelationshipCustomFieldAndLore()
        {
            var world = TestData.CreateWorld();
            world.Characters.Add(new() { Id = "char-2", Name = "상대" });
            world.Characters[0].Relationships.Add(new() { TargetCharacterId = "char-2", Description = "친구" });
            world.Characters[0].CustomFields.Add(new() { Label = "취향", Value = "차" });

            string prompt = new PromptBuilder().Build(
                world,
                world.Characters[0],
                world.UserPersonas[0],
                world.ChatSessions[0],
                world.Characters[0].Lore);

            Assert.Contains("세계", prompt);
            Assert.Contains("캐릭터", prompt);
            Assert.Contains("유저", prompt);
            Assert.Contains("상대: 친구", prompt);
            Assert.Contains("[취향]", prompt);
            Assert.Contains("[로어북 - 현재 대화에 적용되는 설정]", prompt);
        }
    }
}
