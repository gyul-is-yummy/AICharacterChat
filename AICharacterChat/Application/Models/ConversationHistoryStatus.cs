namespace AICharacterChat.Application.Models
{
    public sealed record ConversationHistoryStatus(
        int UnsummarizedOldMessageCount,
        bool HasUnsummarizedOldMessageWarning);
}
