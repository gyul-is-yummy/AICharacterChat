using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AICharacterChat.Application.Models;
using AICharacterChat.Domain.Enums;
using AICharacterChat.Infrastructure.AI.Anthropic;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AICharacterChat.Tests
{
    public class AnthropicClientTests
    {
        [Fact]
        public async Task StructuredResponseFormatMapsToOutputConfigWithJsonObjectSchema()
        {
            var handler = new CaptureHandler();
            var client = new AnthropicClient(new HttpClient(handler), "test-key");
            var request = CreateRequest();
            request.ResponseFormat = new JsonSchemaResponseFormat
            {
                Schema = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["title"] = new Dictionary<string, object> { ["type"] = "string" }
                    },
                    ["required"] = new[] { "title" },
                    ["additionalProperties"] = false
                }
            };

            await client.SendAsync(request);

            var json = JObject.Parse(handler.RequestJson);
            Assert.Equal("json_schema", json["output_config"]!["format"]!["type"]!.Value<string>());
            Assert.Equal(JTokenType.Object, json["output_config"]!["format"]!["schema"]!.Type);
            Assert.Equal("object", json["output_config"]!["format"]!["schema"]!["type"]!.Value<string>());
            Assert.False(json["output_config"]!["format"]!["schema"]!["additionalProperties"]!.Value<bool>());
        }

        [Fact]
        public async Task NormalChatWithoutResponseFormatOmitsOutputConfigAndKeepsExistingFields()
        {
            var handler = new CaptureHandler();
            var client = new AnthropicClient(new HttpClient(handler), "test-key");

            await client.SendAsync(CreateRequest());

            var json = JObject.Parse(handler.RequestJson);
            Assert.Null(json["output_config"]);
            Assert.Equal("model", json["model"]!.Value<string>());
            Assert.Equal(123, json["max_tokens"]!.Value<int>());
            Assert.Equal("system", json["system"]!.Value<string>());
            Assert.Equal("user", json["messages"]![0]!["role"]!.Value<string>());
            Assert.Equal("hello", json["messages"]![0]!["content"]!.Value<string>());
        }

        private static ChatCompletionRequest CreateRequest() =>
            new()
            {
                Model = "model",
                MaxTokens = 123,
                SystemPrompt = "system",
                Messages =
                [
                    new ChatCompletionMessage
                    {
                        Role = ChatRole.User,
                        Content = "hello"
                    }
                ]
            };

        private sealed class CaptureHandler : HttpMessageHandler
        {
            public string RequestJson { get; private set; } = "";

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                RequestJson = await request.Content!.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        {
                          "content": [
                            {
                              "type": "text",
                              "text": "reply"
                            }
                          ]
                        }
                        """)
                };
            }
        }
    }
}
