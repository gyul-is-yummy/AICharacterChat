using System.Linq;
using AICharacterChat.Application.Chat;
using AICharacterChat.Application.Models;
using AICharacterChat.Domain.Enums;
using AICharacterChat.Domain.Models;

namespace AICharacterChat.Application.Context
{
    public class ContextBuilder
    {
        private readonly PromptBuilder _promptBuilder;
        private readonly LoreMatcher _loreMatcher;
        private readonly RecentMessageSelector _recentMessageSelector;

        public ContextBuilder(
            PromptBuilder promptBuilder,
            LoreMatcher loreMatcher,
            RecentMessageSelector recentMessageSelector)
        {
            _promptBuilder = promptBuilder;
            _loreMatcher = loreMatcher;
            _recentMessageSelector = recentMessageSelector;
        }

        public BuiltChatContext Build(
            World world,
            Character character,
            ChatSession session,
            string rawUserInput)
        {
            var userPersona = world.UserPersonas
                .FirstOrDefault(u => u.Id == session.UserPersonaId)
                ?? world.UserPersonas.FirstOrDefault();

            var matchingLore = _loreMatcher.Match(character.Lore, rawUserInput);
            var selectedMessages = _recentMessageSelector.Select(session.Messages);

            return new BuiltChatContext
            {
                SystemPrompt = _promptBuilder.Build(world, character, userPersona, session, matchingLore),
                Messages = selectedMessages
                    .Select(message => new ChatCompletionMessage
                    {
                        Role = message.Role,
                        Content = message.Role == ChatRole.User
                            ? WrapUserInput(message.Content, character.Name)
                            : message.Content
                    })
                    .ToList()
            };
        }

        private static string WrapUserInput(string userInput, string characterName)
        {
            return $"""
                [현재 상황 서술]
                {userInput}

                위 상황에서 {characterName}으로서 반응해주세요.
                행동 묘사와 대사를 함께 포함하여 소설 문체로 답하세요.
                """;
        }
    }
}
