using System.Collections.Generic;
using AICharacterChat.Application.Models;

namespace AICharacterChat.Application.Context
{
    public class BuiltChatContext
    {
        public string SystemPrompt { get; init; } = "";
        public IReadOnlyList<ChatCompletionMessage> Messages { get; init; } = [];
    }
}
