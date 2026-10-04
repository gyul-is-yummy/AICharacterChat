using System;
using AICharacterChat.Application.Models;
using AICharacterChat.Domain.Models;

namespace AICharacterChat.Application.Context
{
    public sealed class ConversationHistoryStatusService
    {
        private readonly RecentMessageSelector _recentMessageSelector;
        private readonly HistoricalContextBuilder _historicalContextBuilder;

        public ConversationHistoryStatusService(
            RecentMessageSelector recentMessageSelector,
            HistoricalContextBuilder historicalContextBuilder)
        {
            _recentMessageSelector = recentMessageSelector;
            _historicalContextBuilder = historicalContextBuilder;
        }

        public ConversationHistoryStatus GetStatus(ChatSession session)
        {
            ArgumentNullException.ThrowIfNull(session);

            var recentMessages = _recentMessageSelector.Select(session.Messages);
            var historicalContext = _historicalContextBuilder.Build(session, recentMessages);
            return new ConversationHistoryStatus(
                historicalContext.UnsummarizedOldMessageCount,
                historicalContext.HasOldUnsummarizedWarning);
        }
    }
}
