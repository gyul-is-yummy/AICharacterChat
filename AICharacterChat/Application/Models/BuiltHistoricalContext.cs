namespace AICharacterChat.Application.Models
{
    public class BuiltHistoricalContext
    {
        public const int OldUnsummarizedWarningThreshold = 20;

        public string Text { get; init; } = "";
        public int UnsummarizedOldMessageCount { get; init; }
        public bool HasOldUnsummarizedWarning =>
            UnsummarizedOldMessageCount >= OldUnsummarizedWarningThreshold;
    }
}
