using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace AICharacterChat
{
    public class WorldManager
    {
        private static readonly string SavePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "worlds.json");

        public List<WorldProfile> Worlds { get; set; } = new();
        public string ActiveWorldId { get; set; } = "";
        public string SelectedModel { get; set; } = "claude-haiku-4-5-20251001";

        [JsonIgnore]
        public WorldProfile? ActiveWorld =>
            Worlds.FirstOrDefault(w => w.Id == ActiveWorldId);

        // ── 불러오기 ──────────────────────────────
        public static WorldManager Load()
        {
            if (!File.Exists(SavePath))
            {
                var manager = new WorldManager();
                var defaultWorld = CreateDefaultWorld();
                manager.Worlds.Add(defaultWorld);
                manager.ActiveWorldId = defaultWorld.Id;
                manager.Save();
                return manager;
            }

            string json = File.ReadAllText(SavePath);
            var result = JsonConvert.DeserializeObject<WorldManager>(json)
                           ?? new WorldManager();

            foreach (var world in result.Worlds)
            {
                // ★ 중복 캐릭터 제거
                world.Characters = world.Characters
                    .GroupBy(c => c.Id)
                    .Select(g => g.First())
                    .ToList();

                // ★ 중복 유저 프로필 제거
                world.UserProfiles = world.UserProfiles
                    .GroupBy(u => u.Id)
                    .Select(g => g.First())
                    .ToList();

                // 중복 대화 기록 제거
                foreach (var character in world.Characters)
                {
                    var seen = new HashSet<string>();
                    var cleaned = new List<ChatMessage>();

                    foreach (var msg in character.ConversationHistory)
                    {
                        string key = msg.Role + msg.Content.Substring(
                            0, Math.Min(50, msg.Content.Length));
                        if (seen.Add(key))
                            cleaned.Add(msg);
                    }
                    character.ConversationHistory = cleaned;

                    // 중복 추가 항목 제거 (Label + Value 동일한 것)
                    character.CustomFields = character.CustomFields
                        .GroupBy(f => f.Label + "\0" + f.Value)
                        .Select(g => g.First())
                        .ToList();

                    // 중복 로어 항목 제거
                    character.Lore = character.Lore
                        .GroupBy(l => l.Id)
                        .Select(g => g.First())
                        .ToList();
                }
            }

            // ★ 중복 세계관 제거
            result.Worlds = result.Worlds
                .GroupBy(w => w.Id)
                .Select(g => g.First())
                .ToList();

            result.Save();
            return result;
        }

        private static WorldProfile CreateDefaultWorld()
        {
            var world = new WorldProfile
            {
                Name = "로맨스 판타지",
                Genre = "로맨스 판타지",
                Era = "현대",
                Description = "기본 세계관입니다."
            };

            // ★ 기본 유저 프로필 추가
            var defaultUser = new UserProfile { Name = "나" };
            world.UserProfiles.Add(defaultUser);

            var ch = new CharacterProfile
            {
                Name = "이준혁",
                Age = "28세",
                Job = "재벌 3세",
                Appearance = "차갑고 날카로운 눈매, 짧은 검은 머리",
                Personality = "겉으로는 냉정하고 무뚝뚝하지만 내면은 따뜻함",
                Etc = "",
                SpeechStyle = "반드시 반말 사용\n감정을 잘 드러내지 않음\n가끔 비꼬는 듯한 말투",
                Situation = "사용자와 계약결혼 관계\n사용자를 내심 신경 쓰지만 절대 티 내지 않음",
                SelectedUserProfileId = defaultUser.Id
            };
            world.Characters.Add(ch);
            world.ActiveCharacterId = ch.Id;
            return world;
        }

        // ── 저장 ──────────────────────────────────
        public void Save()
        {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(SavePath, json);
        }

        // ── 세계관 조작 ───────────────────────────
        public void AddWorld(WorldProfile world)
        {
            Worlds.Add(world);
            Save();
        }

        public void RemoveWorld(string worldId)
        {
            if (Worlds.Count <= 1) return;
            Worlds.RemoveAll(w => w.Id == worldId);
            if (ActiveWorldId == worldId)
                ActiveWorldId = Worlds.FirstOrDefault()?.Id ?? "";
            Save();
        }

        // ── 캐릭터 조작 ───────────────────────────
        public void AddCharacter(CharacterProfile profile)
        {
            ActiveWorld?.Characters.Add(profile);
            Save();
        }

        public void RemoveCharacter(string characterId)
        {
            var world = ActiveWorld;
            if (world == null) return;
            world.Characters.RemoveAll(c => c.Id == characterId);
            if (world.ActiveCharacterId == characterId)
                world.ActiveCharacterId = world.Characters.FirstOrDefault()?.Id ?? "";
            Save();
        }
    }
}