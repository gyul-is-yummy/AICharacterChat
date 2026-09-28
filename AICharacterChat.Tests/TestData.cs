using AICharacterChat.Domain.Models;

namespace AICharacterChat.Tests
{
    internal static class TestData
    {
        public static World CreateWorld()
        {
            var world = new World
            {
                Id = "world-1",
                Name = "세계",
                Genre = "장르",
                Era = "시대",
                Description = "설명",
                Rules = "규칙"
            };
            var persona = new UserPersona { Id = "user-1", Name = "유저" };
            var character = new Character
            {
                Id = "char-1",
                Name = "캐릭터",
                DefaultScenario = "기본 상황",
                Lore = [new LoreEntry { Id = "lore-1", Title = "로어", Keywords = ["키워드"], Content = "로어 내용" }]
            };
            world.UserPersonas.Add(persona);
            world.Characters.Add(character);
            world.ActiveCharacterId = character.Id;
            world.ChatSessions.Add(new ChatSession
            {
                Id = "session-1",
                WorldId = world.Id,
                CharacterId = character.Id,
                UserPersonaId = persona.Id
            });
            return world;
        }

        public static string CreateLegacyJson() => """
            {
              "ActiveWorldId": "world-1",
              "SelectedModel": "legacy-model",
              "Worlds": [
                {
                  "Id": "world-1",
                  "Name": "세계",
                  "Genre": "장르",
                  "Era": "시대",
                  "Description": "설명",
                  "Rules": "규칙",
                  "ActiveCharacterId": "char-1",
                  "UserProfiles": [
                    {
                      "Id": "user-1",
                      "Name": "유저",
                      "Appearance": "외형",
                      "Personality": "성격",
                      "AdditionalInfo": "추가"
                    }
                  ],
                  "Characters": [
                    {
                      "Id": "char-1",
                      "Name": "캐릭터",
                      "Age": "20",
                      "Gender": "여",
                      "Job": "직업",
                      "Appearance": "캐릭터 외형",
                      "Personality": "캐릭터 성격",
                      "Etc": "기타",
                      "Secret": "비밀",
                      "SpeechStyle": "말투",
                      "Situation": "기본 상황",
                      "SelectedUserProfileId": "user-1",
                      "Relationships": [
                        { "TargetCharacterId": "char-2", "Description": "관계" }
                      ],
                      "CustomFields": [
                        { "Label": "취향", "Value": "차" }
                      ],
                      "Lore": [
                        { "Id": "lore-1", "Title": "로어", "Keywords": ["키"], "Content": "내용", "IsEnabled": true }
                      ],
                      "ConversationHistory": [
                        {
                          "role": "user",
                          "content": "[현재 상황 서술]\r\n원래 입력\r\n\r\n위 상황에서 캐릭터로서 반응해주세요."
                        },
                        {
                          "role": "assistant",
                          "content": "응답"
                        },
                        {
                          "role": "user",
                          "content": "[현재 상황 서술]\r\n반복 입력\r\n\r\n위 상황에서 캐릭터로서 반응해주세요."
                        },
                        {
                          "role": "user",
                          "content": "[현재 상황 서술]\r\n반복 입력\r\n\r\n위 상황에서 캐릭터로서 반응해주세요."
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;
    }
}
