using AICharacterChat.Domain.Models;

namespace AICharacterChat.Application.Models
{
    public class SummaryServiceResult
    {
        public bool IsSuccess { get; init; }
        public string? ErrorMessage { get; init; }
        public ConversationSummary? Summary { get; init; }

        public static SummaryServiceResult Success(ConversationSummary? summary = null) =>
            new() { IsSuccess = true, Summary = summary };

        public static SummaryServiceResult Failure(string message) =>
            new() { IsSuccess = false, ErrorMessage = message };
    }
}
