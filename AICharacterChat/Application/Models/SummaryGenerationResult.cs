namespace AICharacterChat.Application.Models
{
    public sealed class SummaryGenerationResult
    {
        public bool IsSuccess { get; init; }
        public bool IsCanceled { get; init; }
        public string? ErrorMessage { get; init; }
        public SummaryDraft? Draft { get; init; }

        public static SummaryGenerationResult Success(SummaryDraft draft) =>
            new() { IsSuccess = true, Draft = draft };

        public static SummaryGenerationResult Failure(string message) =>
            new() { IsSuccess = false, ErrorMessage = message };

        public static SummaryGenerationResult Canceled() =>
            new() { IsSuccess = false, IsCanceled = true };
    }
}
