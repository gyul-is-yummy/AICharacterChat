using System.Collections.Generic;
using System.Linq;
using System.Text;
using AICharacterChat.Domain.Models;

namespace AICharacterChat.Application.Chat
{
    public class PromptBuilder
    {
        public string Build(
            World? world,
            Character character,
            UserPersona? userPersona,
            ChatSession session,
            IReadOnlyList<AICharacterChat.Domain.Models.LoreEntry>? matchingLore = null)
        {
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

            string relText = BuildRelationshipText(world, character);
            string userName = userPersona?.Name ?? "나";
            string userDetail = BuildUserDetail(userPersona);
            string customFields = BuildCustomFields(character.CustomFields);
            string scenario = string.IsNullOrWhiteSpace(session.Scenario)
                ? character.DefaultScenario
                : session.Scenario;
            string loreSection = BuildLoreSection(matchingLore);

            return $"""
                당신은 '{character.Name}'입니다.

                {worldContext}

                [기본 정보]
                나이: {character.Age}
                성별: {character.Gender}
                직업: {character.Job}

                [외형]
                {character.Appearance}

                [성격]
                {character.Personality}

                [다른 캐릭터와의 관계]
                {relText}

                [기타]
                {character.Etc}

                [비밀 - 절대 직접 발설하지 말 것, 행동과 분위기로만 암시]
                {character.Secret}

                [말투 규칙]
                {character.SpeechStyle}

                {customFields}

                [상황 설정]
                {scenario}

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

        private static string BuildRelationshipText(World? world, Character character)
        {
            if (world == null)
                return "(설정된 관계 없음)";

            var relBuilder = new StringBuilder();
            foreach (var rel in character.Relationships)
            {
                var target = world.Characters.FirstOrDefault(c => c.Id == rel.TargetCharacterId);
                if (target != null && !string.IsNullOrWhiteSpace(rel.Description))
                    relBuilder.AppendLine($"- {target.Name}: {rel.Description}");
            }

            return relBuilder.Length > 0
                ? relBuilder.ToString().Trim()
                : "(설정된 관계 없음)";
        }

        private static string BuildUserDetail(UserPersona? userPersona)
        {
            var userParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(userPersona?.Appearance))
                userParts.Add($"외형: {userPersona!.Appearance}");
            if (!string.IsNullOrWhiteSpace(userPersona?.Personality))
                userParts.Add($"성격: {userPersona!.Personality}");
            if (!string.IsNullOrWhiteSpace(userPersona?.AdditionalInfo))
                userParts.Add(userPersona!.AdditionalInfo);

            return userParts.Count > 0
                ? string.Join("\n", userParts)
                : "(추가 설정 없음)";
        }

        private static string BuildCustomFields(IEnumerable<AICharacterChat.Domain.Models.CustomField> customFields)
        {
            var builder = new StringBuilder();
            foreach (var field in customFields)
            {
                if (!string.IsNullOrWhiteSpace(field.Label) &&
                    !string.IsNullOrWhiteSpace(field.Value))
                    builder.AppendLine($"[{field.Label}]\n{field.Value}");
            }

            return builder.ToString().TrimEnd();
        }

        private static string BuildLoreSection(IReadOnlyList<AICharacterChat.Domain.Models.LoreEntry>? matchingLore)
        {
            if (matchingLore == null || matchingLore.Count == 0)
                return "";

            var loreBuilder = new StringBuilder("\n[로어북 - 현재 대화에 적용되는 설정]\n");
            foreach (var entry in matchingLore)
                loreBuilder.AppendLine($"# {entry.Title}\n{entry.Content}");

            return loreBuilder.ToString().TrimEnd();
        }
    }
}
