using System;

namespace AICharacterChat.Domain.Models
{
    public sealed class ConversationSummary
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid StartMessageId { get; set; }
        public Guid EndMessageId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string CurrentSituation { get; set; } = "없음";
        public string KeyEvents { get; set; } = "없음";
        public string RelationshipChanges { get; set; } = "없음";
        public string PromisesAndImportantStatements { get; set; } = "없음";
        public string UnresolvedMatters { get; set; } = "없음";
        public string PersistentState { get; set; } = "없음";
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
        public int Revision { get; set; } = 1;
    }
}
