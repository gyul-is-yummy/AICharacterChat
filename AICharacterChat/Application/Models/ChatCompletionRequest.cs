using System.Collections.Generic;

namespace AICharacterChat.Application.Models
{
    public class ChatCompletionRequest
    {
        public string Model { get; set; } = "";
        public int MaxTokens { get; set; } = 1024;
        public string SystemPrompt { get; set; } = "";
        public IReadOnlyList<ChatCompletionMessage> Messages { get; set; } = [];
    }
}
