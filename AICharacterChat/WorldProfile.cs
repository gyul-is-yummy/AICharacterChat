using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace AICharacterChat
{
    public class WorldProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "새 세계관";
        public string Genre { get; set; } = "";
        public string Era { get; set; } = "";
        public string Description { get; set; } = "";
        public string Rules { get; set; } = "";

        public List<CharacterProfile> Characters { get; set; } = new();
        public string ActiveCharacterId { get; set; } = "";

        // ★ 추가
        public List<UserProfile> UserProfiles { get; set; } = new();

        [JsonIgnore]
        public CharacterProfile? ActiveCharacter =>
            Characters.FirstOrDefault(c => c.Id == ActiveCharacterId);
    }
}