using System.Collections.Generic;
using Newtonsoft.Json;

namespace AICharacterChat.Infrastructure.AI.Anthropic
{
    public class AnthropicResponse
    {
        [JsonProperty("content")]
        public List<AnthropicContent> Content { get; set; } = new();
    }

    public class AnthropicContent
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "";

        [JsonProperty("text")]
        public string Text { get; set; } = "";
    }

    public class AnthropicErrorResponse
    {
        [JsonProperty("error")]
        public AnthropicError? Error { get; set; }
    }

    public class AnthropicError
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "";

        [JsonProperty("message")]
        public string Message { get; set; } = "";
    }
}
