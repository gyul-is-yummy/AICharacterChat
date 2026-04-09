using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AICharacterChat
{
    public class CharacterProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "새 캐릭터";
        public string Age { get; set; } = "";
        public string Gender { get; set; } = "";   // ★ 추가
        public string Job { get; set; } = "";
        public string Appearance { get; set; } = "";
        public string Personality { get; set; } = "";
        public string Etc { get; set; } = "";
        public string Secret { get; set; } = "";
        public string SpeechStyle { get; set; } = "반드시 반말 사용\n감정을 잘 드러내지 않음";
        public string Situation { get; set; } = "";

        // ★ 추가 항목
        public List<CustomField> CustomFields { get; set; } = new();

        public string SelectedUserProfileId { get; set; } = "";

        public List<CharacterRelationship> Relationships { get; set; } = new();
        public List<ChatMessage> ConversationHistory { get; set; } = new();
        public List<LoreEntry> Lore { get; set; } = new();

        public string BuildSystemPrompt(
            WorldProfile? world,
            List<CharacterProfile> worldCharacters,
            UserProfile? userProfile,
            List<LoreEntry>? matchingLore = null)
        {
            // 세계관
            string worldContext = "";
            if (world != null)
            {
                worldContext = $"""
                    [세계관 배경]
                    이름: {world.Name}
                    장르: {world.Genre}
                    시대: {world.Era}
                    설명: {world.Description}
                    규칙: {world.Rules}
                    """;
            }

            // 관계
            var relBuilder = new StringBuilder();
            foreach (var rel in Relationships)
            {
                var target = worldCharacters.FirstOrDefault(c => c.Id == rel.TargetCharacterId);
                if (target != null && !string.IsNullOrWhiteSpace(rel.Description))
                    relBuilder.AppendLine($"- {target.Name}: {rel.Description}");
            }
            string relText = relBuilder.Length > 0
                ? relBuilder.ToString().Trim()
                : "(설정된 관계 없음)";

            // 유저
            string userName = userProfile?.Name ?? "나";
            var userParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(userProfile?.Appearance))
                userParts.Add($"외형: {userProfile!.Appearance}");
            if (!string.IsNullOrWhiteSpace(userProfile?.Personality))
                userParts.Add($"성격: {userProfile!.Personality}");
            if (!string.IsNullOrWhiteSpace(userProfile?.AdditionalInfo))
                userParts.Add(userProfile!.AdditionalInfo);
            string userDetail = userParts.Count > 0
                ? string.Join("\n", userParts)
                : "(추가 설정 없음)";

            // ★ 추가 항목
            var customBuilder = new StringBuilder();
            foreach (var field in CustomFields)
            {
                if (!string.IsNullOrWhiteSpace(field.Label) &&
                    !string.IsNullOrWhiteSpace(field.Value))
                    customBuilder.AppendLine($"[{field.Label}]\n{field.Value}");
            }

            // 로어북 섹션
            string loreSection = "";
            if (matchingLore != null && matchingLore.Count > 0)
            {
                var loreBuilder = new StringBuilder("\n[로어북 - 현재 대화에 적용되는 설정]\n");
                foreach (var entry in matchingLore)
                    loreBuilder.AppendLine($"# {entry.Title}\n{entry.Content}");
                loreSection = loreBuilder.ToString().TrimEnd();
            }

            return $"""
                당신은 '{Name}'입니다.

                {worldContext}

                [기본 정보]
                나이: {Age}
                성별: {Gender}
                직업: {Job}

                [외형]
                {Appearance}

                [성격]
                {Personality}

                [다른 캐릭터와의 관계]
                {relText}

                [기타]
                {Etc}

                [비밀 - 절대 직접 발설하지 말 것, 행동과 분위기로만 암시]
                {Secret}

                [말투 규칙]
                {SpeechStyle}

                {customBuilder}

                [상황 설정]
                {Situation}

                [대화 상대 설정]
                이름: {userName}
                {userDetail}

                [입력 형식]
                사용자는 자신의 내면 심리, 감정, 현재 상황을 서술합니다.

                [응답 규칙]
                - 사용자의 서술을 소설처럼 읽고 그 상황에 맞게 반응할 것
                - 대사만이 아니라 행동 묘사도 함께 포함할 것
                  예시: 그가 천천히 고개를 들었다. "...알고 있어."
                - 대화 상대를 지칭할 때는 '{userName}' 또는 설정에 맞는 호칭을 사용할 것
                - 응답은 소설 문체로, 3~6문장 내외로 작성할 것
                - AI임을 절대 언급하지 말 것
                - 한국어로만 대화할 것
                {loreSection}
                """;
        }
    }
}