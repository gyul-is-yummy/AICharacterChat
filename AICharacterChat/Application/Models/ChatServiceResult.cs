namespace AICharacterChat.Application.Models
{
    public class ChatServiceResult
    {
        public bool IsSuccess { get; init; }
        public bool IsCanceled { get; init; }
        public string? Reply { get; init; }
        public string? ErrorMessage { get; init; }

        public static ChatServiceResult Success(string reply) =>
            new() { IsSuccess = true, Reply = reply };

        public static ChatServiceResult Failure(string message) =>
            new() { IsSuccess = false, ErrorMessage = message };

        public static ChatServiceResult Canceled() =>
            new() { IsSuccess = false, IsCanceled = true, ErrorMessage = "요청이 취소되었습니다." };
    }
}
