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

        [JsonProperty("output_config", NullValueHandling = NullValueHandling.Ignore)]
        public AnthropicOutputConfig? OutputConfig { get; set; }
    }

    public class AnthropicMessage
    {
        [JsonProperty("role")]
        public string Role { get; set; } = "";

        [JsonProperty("content")]
        public string Content { get; set; } = "";
    }

    public class AnthropicOutputConfig
    {
        [JsonProperty("format")]
        public AnthropicOutputFormat Format { get; set; } = new();
    }

    public class AnthropicOutputFormat
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "json_schema";

        [JsonProperty("schema")]
        public object Schema { get; set; } = new();
    }
}
