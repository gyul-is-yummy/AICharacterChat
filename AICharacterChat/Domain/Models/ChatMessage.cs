using System;
using AICharacterChat.Domain.Enums;

namespace AICharacterChat.Domain.Models
{
    public class ChatMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public ChatRole Role { get; set; }
        public string Content { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

        public ChatMessage()
        {
        }

        public ChatMessage(ChatRole role, string content)
        {
            Role = role;
            Content = content;
        }
    }
}
