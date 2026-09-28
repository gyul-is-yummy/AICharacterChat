using System.Collections.Generic;
using AICharacterChat.Application.Interfaces;
using AICharacterChat.Application.Models;

namespace AICharacterChat.Infrastructure.AI.Anthropic
{
    public class AnthropicModelCatalog : IChatModelCatalog
    {
        public IReadOnlyList<ChatModelOption> Models { get; } =
        [
            new() { Id = "claude-haiku-4-5-20251001", Label = "Haiku 4.5 (빠름/저렴)" },
            new() { Id = "claude-sonnet-4-6", Label = "Sonnet 4.6 (균형)" },
            new() { Id = "claude-opus-4-6", Label = "Opus 4.6 (고성능)" },
        ];
    }
}
