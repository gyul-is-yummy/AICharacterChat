using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AICharacterChat.Application.Interfaces;
using AICharacterChat.Application.Models;
using AICharacterChat.Domain.Enums;
using Newtonsoft.Json;

namespace AICharacterChat.Infrastructure.AI.Anthropic
{
    public class AnthropicClient : IChatModelClient
    {
        private const string Endpoint = "https://api.anthropic.com/v1/messages";
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public AnthropicClient(HttpClient httpClient)
            : this(httpClient, Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "")
        {
        }

        public AnthropicClient(HttpClient httpClient, string apiKey)
        {
            _httpClient = httpClient;
            _apiKey = apiKey;
        }

        public async Task<string> SendAsync(
            ChatCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new ChatModelException("ANTHROPIC_API_KEY 환경 변수가 설정되어 있지 않습니다.");

            var anthropicRequest = new AnthropicRequest
            {
                Model = request.Model,
                MaxTokens = request.MaxTokens,
                System = request.SystemPrompt,
                Messages = request.Messages.Select(m => new AnthropicMessage
                {
                    Role = m.Role == ChatRole.User ? "user" : "assistant",
                    Content = m.Content
                }).ToList(),
                OutputConfig = request.ResponseFormat == null
                    ? null
                    : new AnthropicOutputConfig
                    {
                        Format = new AnthropicOutputFormat
                        {
                            Schema = request.ResponseFormat.Schema
                        }
                    }
            };

            string json = JsonConvert.SerializeObject(anthropicRequest);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Add("x-api-key", _apiKey);
            httpRequest.Headers.Add("anthropic-version", "2023-06-01");

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new ChatModelException("네트워크 오류로 AI 응답을 가져오지 못했습니다.", null, ex);
            }

            string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var error = JsonConvert.DeserializeObject<AnthropicErrorResponse>(responseJson);
                string? message = error?.Error?.Message;
                if (string.IsNullOrWhiteSpace(message))
                    message = $"Anthropic API 오류가 발생했습니다. HTTP {(int)response.StatusCode}";
                throw new ChatModelException(message, (int)response.StatusCode);
            }

            var result = JsonConvert.DeserializeObject<AnthropicResponse>(responseJson);
            string? text = result?.Content.FirstOrDefault(c => c.Type == "text" || !string.IsNullOrWhiteSpace(c.Text))?.Text;
            if (string.IsNullOrWhiteSpace(text))
                throw new ChatModelException("AI 응답 본문이 비어 있습니다.", (int)response.StatusCode);

            return text;
        }
    }
}
