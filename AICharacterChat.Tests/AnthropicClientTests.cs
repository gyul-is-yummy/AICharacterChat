using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AICharacterChat.Application.Interfaces;
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
            var client = new AnthropicClient(new HttpClient(handler), new FakeApiKeyProvider("test-key"));
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
            var client = new AnthropicClient(new HttpClient(handler), new FakeApiKeyProvider("test-key"));

            await client.SendAsync(CreateRequest());

            var json = JObject.Parse(handler.RequestJson);
            Assert.Equal("test-key", handler.ApiKeyHeader);
            Assert.Null(json["output_config"]);
            Assert.Equal("model", json["model"]!.Value<string>());
            Assert.Equal(123, json["max_tokens"]!.Value<int>());
            Assert.Equal("system", json["system"]!.Value<string>());
            Assert.Equal("user", json["messages"]![0]!["role"]!.Value<string>());
            Assert.Equal("hello", json["messages"]![0]!["content"]!.Value<string>());
        }

        [Fact]
        public async Task MissingApiKeyFailsBeforeHttpRequest()
        {
            var handler = new CaptureHandler();
            var client = new AnthropicClient(new HttpClient(handler), new FakeApiKeyProvider(null));

            var ex = await Assert.ThrowsAsync<ChatModelException>(() => client.SendAsync(CreateRequest()));

            Assert.Contains("API Key", ex.Message);
            Assert.Equal(0, handler.SendCount);
        }

        [Fact]
        public async Task UnreadableApiKeyFailsSafelyBeforeHttpRequest()
        {
            var handler = new CaptureHandler();
            var provider = new FakeApiKeyProvider("super-secret-test-key")
            {
                ReadException = new AnthropicApiKeyReadException("저장된 Anthropic API Key를 읽을 수 없습니다. API 설정에서 키를 다시 저장해 주세요.")
            };
            var client = new AnthropicClient(new HttpClient(handler), provider);

            var ex = await Assert.ThrowsAsync<ChatModelException>(() => client.SendAsync(CreateRequest()));

            Assert.DoesNotContain("super-secret-test-key", ex.Message);
            Assert.Contains("읽을 수 없습니다", ex.Message);
            Assert.Equal(0, handler.SendCount);
        }

        [Fact]
        public async Task ReadsCurrentApiKeyForEachRequest()
        {
            var handler = new CaptureHandler();
            var provider = new FakeApiKeyProvider("first-key");
            var client = new AnthropicClient(new HttpClient(handler), provider);

            await client.SendAsync(CreateRequest());
            provider.ApiKey = "second-key";
            await client.SendAsync(CreateRequest());

            Assert.Equal(2, provider.ReadCount);
            Assert.Equal("second-key", handler.ApiKeyHeader);
            Assert.Equal(2, handler.SendCount);
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
            public string? ApiKeyHeader { get; private set; }
            public int SendCount { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                SendCount++;
                ApiKeyHeader = request.Headers.GetValues("x-api-key").FirstOrDefault();
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

        private sealed class FakeApiKeyProvider : IAnthropicApiKeyProvider
        {
            public FakeApiKeyProvider(string? apiKey)
            {
                ApiKey = apiKey;
            }

            public string? ApiKey { get; set; }
            public AnthropicApiKeyReadException? ReadException { get; set; }
            public int ReadCount { get; private set; }

            public Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default)
            {
                ReadCount++;
                if (ReadException != null)
                    throw ReadException;

                return Task.FromResult(ApiKey);
            }
        }
    }
}
