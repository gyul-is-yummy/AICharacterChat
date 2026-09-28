using System;
using System.Collections.Generic;

namespace AICharacterChat.Domain.Models
{
    public class ChatSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string WorldId { get; set; } = "";
        public string CharacterId { get; set; } = "";
        public string UserPersonaId { get; set; } = "";
        public string Scenario { get; set; } = "";
        public List<ChatMessage> Messages { get; set; } = new();
        public List<ConversationSummary> Summaries { get; set; } = new();
    }
}
