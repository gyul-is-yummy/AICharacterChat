using AICharacterChat.Domain.Models;

namespace AICharacterChat.Application.Models
{
    public sealed class SummaryPersistenceResult
    {
        public bool IsSuccess { get; init; }
        public bool IsCanceled { get; init; }
        public string? ErrorMessage { get; init; }
        public ConversationSummary? Summary { get; init; }

        public static SummaryPersistenceResult Success(ConversationSummary summary) =>
            new() { IsSuccess = true, Summary = summary };

        public static SummaryPersistenceResult Failure(string message) =>
            new() { IsSuccess = false, ErrorMessage = message };

        public static SummaryPersistenceResult Canceled() =>
            new() { IsSuccess = false, IsCanceled = true, ErrorMessage = "요청이 취소되었습니다." };
    }
}
