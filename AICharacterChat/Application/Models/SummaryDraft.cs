using System;

namespace AICharacterChat.Application.Models
{
    public sealed class SummaryDraft
    {
        public Guid StartMessageId { get; set; }
        public Guid EndMessageId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string CurrentSituation { get; set; } = "없음";
        public string KeyEvents { get; set; } = "없음";
        public string RelationshipChanges { get; set; } = "없음";
        public string PromisesAndImportantStatements { get; set; } = "없음";
        public string UnresolvedMatters { get; set; } = "없음";
        public string PersistentState { get; set; } = "없음";
    }
}
