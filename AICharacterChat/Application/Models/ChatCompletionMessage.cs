using AICharacterChat.Domain.Enums;

namespace AICharacterChat.Application.Models
{
    public class ChatCompletionMessage
    {
        public ChatRole Role { get; set; }
        public string Content { get; set; } = "";
    }
}
