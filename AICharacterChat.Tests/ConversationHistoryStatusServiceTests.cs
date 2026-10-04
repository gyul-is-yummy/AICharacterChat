using System.Linq;
using AICharacterChat.Application.Context;
using AICharacterChat.Domain.Enums;
using AICharacterChat.Domain.Models;
using Xunit;

namespace AICharacterChat.Tests
{
    public class ConversationHistoryStatusServiceTests
    {
        [Fact]
        public void ReturnsCountFromHistoricalContextBuilderSemantics()
        {
            var session = CreateSession(30);
            session.Summaries.Add(CreateSummary(session, 1, 5, "요약"));
            var service = CreateService();

            var result = service.GetStatus(session);

            Assert.Equal(5, result.UnsummarizedOldMessageCount);
            Assert.False(result.HasUnsummarizedOldMessageWarning);
        }

        [Fact]
        public void BelowThresholdDoesNotWarn()
        {
            var session = CreateSession(39);
            var service = CreateService();

            var result = service.GetStatus(session);

            Assert.Equal(19, result.UnsummarizedOldMessageCount);
            Assert.False(result.HasUnsummarizedOldMessageWarning);
        }

        [Fact]
        public void AtThresholdWarns()
        {
            var session = CreateSession(40);
            var service = CreateService();

            var result = service.GetStatus(session);

            Assert.Equal(20, result.UnsummarizedOldMessageCount);
            Assert.True(result.HasUnsummarizedOldMessageWarning);
        }

        [Fact]
        public void DoesNotMutateSessionMessagesOrSummaries()
        {
            var session = CreateSession(30);
            var summary = CreateSummary(session, 1, 5, "요약");
            session.Summaries.Add(summary);
            var messageSnapshot = session.Messages.Select(message => new { message.Id, message.Role, message.Content }).ToList();
            var summarySnapshot = session.Summaries.ToList();
            var service = CreateService();

            _ = service.GetStatus(session);

            Assert.Equal(messageSnapshot.Select(message => message.Id), session.Messages.Select(message => message.Id));
            Assert.Equal(messageSnapshot.Select(message => message.Role), session.Messages.Select(message => message.Role));
            Assert.Equal(messageSnapshot.Select(message => message.Content), session.Messages.Select(message => message.Content));
            Assert.Equal(summarySnapshot, session.Summaries);
        }

        private static ConversationHistoryStatusService CreateService() =>
            new(new RecentMessageSelector(), new HistoricalContextBuilder());

        private static ChatSession CreateSession(int count) =>
            new()
            {
                Messages = Enumerable.Range(1, count)
                    .Select(index => new ChatMessage(ChatRole.User, $"message-{index}"))
                    .ToList()
            };

        private static ConversationSummary CreateSummary(ChatSession session, int startOneBased, int endOneBased, string title) =>
            new()
            {
                StartMessageId = session.Messages[startOneBased - 1].Id,
                EndMessageId = session.Messages[endOneBased - 1].Id,
                Title = title,
                CurrentSituation = "상황",
                KeyEvents = "사건",
                RelationshipChanges = "관계",
                PromisesAndImportantStatements = "약속",
                UnresolvedMatters = "미해결",
                PersistentState = "상태"
            };
    }
}
