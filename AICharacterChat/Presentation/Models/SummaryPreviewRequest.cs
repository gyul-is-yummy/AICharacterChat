using System;
using AICharacterChat.Domain.Models;

namespace AICharacterChat.Presentation.Models
{
    public sealed record SummaryPreviewRequest(
        WorldStore Store,
        ChatSession Session,
        Character Character,
        Guid StartMessageId,
        Guid EndMessageId,
        string ModelId);
}
