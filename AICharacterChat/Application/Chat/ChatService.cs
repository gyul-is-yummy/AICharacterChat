using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AICharacterChat.Application.Interfaces;
using AICharacterChat.Application.Models;
using AICharacterChat.Domain.Enums;
using AICharacterChat.Domain.Models;

namespace AICharacterChat.Application.Chat
{
    public class ChatService
    {
        private readonly IChatModelClient _chatModelClient;
        private readonly IWorldRepository _worldRepository;
        private readonly PromptBuilder _promptBuilder;
        private readonly LoreMatcher _loreMatcher;

        public ChatService(
            IChatModelClient chatModelClient,
            IWorldRepository worldRepository,
            PromptBuilder promptBuilder,
            LoreMatcher loreMatcher)
        {
            _chatModelClient = chatModelClient;
            _worldRepository = worldRepository;
            _promptBuilder = promptBuilder;
            _loreMatcher = loreMatcher;
        }

        public async Task<ChatServiceResult> SendAsync(
            WorldStore store,
            World world,
            Character character,
            ChatSession session,
            string rawUserInput,
            string model,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(rawUserInput))
                return ChatServiceResult.Failure("메시지를 입력해주세요.");

            int originalMessageCount = session.Messages.Count;
            var userMessage = new AICharacterChat.Domain.Models.ChatMessage(ChatRole.User, rawUserInput);
            session.Messages.Add(userMessage);

            try
            {
                var userPersona = world.UserPersonas
                    .FirstOrDefault(u => u.Id == session.UserPersonaId)
                    ?? world.UserPersonas.FirstOrDefault();

                var matchingLore = _loreMatcher.Match(character.Lore, rawUserInput);
                var request = new ChatCompletionRequest
                {
                    Model = model,
                    MaxTokens = 1024,
                    SystemPrompt = _promptBuilder.Build(world, character, userPersona, session, matchingLore),
                    Messages = session.Messages
                        .Select(message => new ChatCompletionMessage
                        {
                            Role = message.Role,
                            Content = message.Role == ChatRole.User
                                ? WrapUserInput(message.Content, character.Name)
                                : message.Content
                        })
                        .ToList()
                };

                string reply = await _chatModelClient.SendAsync(request, cancellationToken);
                session.Messages.Add(new AICharacterChat.Domain.Models.ChatMessage(ChatRole.Assistant, reply));
                await _worldRepository.SaveAsync(store, cancellationToken);
                return ChatServiceResult.Success(reply);
            }
            catch (OperationCanceledException)
            {
                RollbackMessages(session, originalMessageCount);
                return ChatServiceResult.Canceled();
            }
            catch (Exception ex)
            {
                RollbackMessages(session, originalMessageCount);
                return ChatServiceResult.Failure(ex.Message);
            }
        }

        private static void RollbackMessages(ChatSession session, int originalMessageCount)
        {
            while (session.Messages.Count > originalMessageCount)
                session.Messages.RemoveAt(session.Messages.Count - 1);
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
