using System;
using System.Collections.Generic;

namespace AICharacterChat.Domain.Models
{
    public class Character
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "새 캐릭터";
        public string Age { get; set; } = "";
        public string Gender { get; set; } = "";
        public string Job { get; set; } = "";
        public string Appearance { get; set; } = "";
        public string Personality { get; set; } = "";
        public string Etc { get; set; } = "";
        public string Secret { get; set; } = "";
        public string SpeechStyle { get; set; } = "반드시 반말 사용\n감정을 잘 드러내지 않음";
        public string DefaultScenario { get; set; } = "";
        public List<CustomField> CustomFields { get; set; } = new();
        public List<CharacterRelationship> Relationships { get; set; } = new();
        public List<LoreEntry> Lore { get; set; } = new();
    }
}
