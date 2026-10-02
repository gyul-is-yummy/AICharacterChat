using AICharacterChat.Domain.Models;

namespace AICharacterChat.Presentation.Models
{
    public sealed record SummaryManagementRequest(
        WorldStore Store,
        ChatSession Session,
        Character Character,
        string ModelId);
}
