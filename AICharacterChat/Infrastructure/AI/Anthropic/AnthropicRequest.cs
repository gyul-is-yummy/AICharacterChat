using System.Collections.Generic;
using Newtonsoft.Json;

namespace AICharacterChat.Infrastructure.AI.Anthropic
{
    public class AnthropicRequest
    {
        [JsonProperty("model")]
        public string Model { get; set; } = "";

        [JsonProperty("max_tokens")]
        public int MaxTokens { get; set; }

        [JsonProperty("system")]
        public string System { get; set; } = "";

        [JsonProperty("messages")]
        public List<AnthropicMessage> Messages { get; set; } = new();
    }

    public class AnthropicMessage
    {
        [JsonProperty("role")]
        public string Role { get; set; } = "";

        [JsonProperty("content")]
        public string Content { get; set; } = "";
    }
}
