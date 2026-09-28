using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace AICharacterChat.Domain.Models
{
    public class World
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "새 세계관";
        public string Genre { get; set; } = "";
        public string Era { get; set; } = "";
        public string Description { get; set; } = "";
        public string Rules { get; set; } = "";
        public List<Character> Characters { get; set; } = new();
        public string ActiveCharacterId { get; set; } = "";
        public List<UserPersona> UserPersonas { get; set; } = new();
        public List<ChatSession> ChatSessions { get; set; } = new();

        [JsonIgnore]
        public Character? ActiveCharacter =>
            Characters.FirstOrDefault(c => c.Id == ActiveCharacterId);

        public ChatSession? GetSessionForCharacter(string characterId)
        {
            return ChatSessions.FirstOrDefault(s => s.CharacterId == characterId);
        }
    }
}
