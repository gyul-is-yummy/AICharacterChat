using System;
using System.Linq;
using AICharacterChat.Application.Summaries;
using AICharacterChat.Domain.Enums;
using AICharacterChat.Domain.Models;
using Xunit;

namespace AICharacterChat.Tests
{
    public class ConversationSummaryServiceTests
    {
        [Fact]
        public void ValidRangeAccepted()
        {
            var session = CreateSession(5);
            var summary = CreateSummary(session, 1, 3);

            var result = new ConversationSummaryService().AddSummary(session, summary);

            Assert.True(result.IsSuccess);
            Assert.Single(session.Summaries);
        }

        [Fact]
        public void SingleMessageRangeAccepted()
        {
            var session = CreateSession(3);
            var summary = CreateSummary(session, 1, 1);

            var result = new ConversationSummaryService().AddSummary(session, summary);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void StartAfterEndRejected()
        {
            var session = CreateSession(3);

            var result = new ConversationSummaryService().AddSummary(session, CreateSummary(session, 2, 1));

            Assert.False(result.IsSuccess);
            Assert.Empty(session.Summaries);
        }

        [Fact]
        public void MissingStartMessageRejected()
        {
            var session = CreateSession(3);
            var summary = CreateSummary(session, 1, 2);
            summary.StartMessageId = Guid.NewGuid();

            var result = new ConversationSummaryService().AddSummary(session, summary);

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void MissingEndMessageRejected()
        {
            var session = CreateSession(3);
            var summary = CreateSummary(session, 1, 2);
            summary.EndMessageId = Guid.NewGuid();

            var result = new ConversationSummaryService().AddSummary(session, summary);

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void OverlapAtFrontRejected()
        {
            var session = CreateSession(10);
            var service = new ConversationSummaryService();
            service.AddSummary(session, CreateSummary(session, 3, 6));

            var result = service.AddSummary(session, CreateSummary(session, 1, 4));

            Assert.False(result.IsSuccess);
            Assert.Single(session.Summaries);
        }

        [Fact]
        public void OverlapAtBackRejected()
        {
            var session = CreateSession(10);
            var service = new ConversationSummaryService();
            service.AddSummary(session, CreateSummary(session, 3, 6));

            var result = service.AddSummary(session, CreateSummary(session, 5, 8));

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void NewRangeInsideExistingRejected()
        {
            var session = CreateSession(10);
            var service = new ConversationSummaryService();
            service.AddSummary(session, CreateSummary(session, 2, 8));

            var result = service.AddSummary(session, CreateSummary(session, 4, 6));

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void NewRangeContainingExistingRejected()
        {
            var session = CreateSession(10);
            var service = new ConversationSummaryService();
            service.AddSummary(session, CreateSummary(session, 4, 6));

            var result = service.AddSummary(session, CreateSummary(session, 2, 8));

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void AdjacentRangeAllowed()
        {
            var session = CreateSession(10);
            var service = new ConversationSummaryService();
            service.AddSummary(session, CreateSummary(session, 1, 5));

            var result = service.AddSummary(session, CreateSummary(session, 6, 10));

            Assert.True(result.IsSuccess);
            Assert.Equal(2, session.Summaries.Count);
        }

        [Fact]
        public void GapAllowed()
        {
            var session = CreateSession(10);
            var service = new ConversationSummaryService();
            service.AddSummary(session, CreateSummary(session, 1, 3));

            var result = service.AddSummary(session, CreateSummary(session, 7, 10));

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void EmptyTitleRejected()
        {
            var session = CreateSession(3);
            var summary = CreateSummary(session, 1, 2);
            summary.Title = " ";

            var result = new ConversationSummaryService().AddSummary(session, summary);

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void TitleLongerThan40Rejected()
        {
            var session = CreateSession(3);
            var summary = CreateSummary(session, 1, 2);
            summary.Title = new string('가', 41);

            var result = new ConversationSummaryService().AddSummary(session, summary);

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void DeleteDoesNotDeleteMessages()
        {
            var session = CreateSession(3);
            var service = new ConversationSummaryService();
            var summary = CreateSummary(session, 1, 2);
            service.AddSummary(session, summary);

            var result = service.DeleteSummary(session, summary.Id);

            Assert.True(result.IsSuccess);
            Assert.Empty(session.Summaries);
            Assert.Equal(3, session.Messages.Count);
        }

        [Fact]
        public void EditIncrementsRevision()
        {
            var session = CreateSession(3);
            var service = new ConversationSummaryService();
            var summary = CreateSummary(session, 1, 2);
            service.AddSummary(session, summary);

            service.UpdateSummaryContent(session, summary.Id, "수정", "상황", "사건", "관계", "약속", "미해결", "상태");

            Assert.Equal(2, summary.Revision);
        }

        [Fact]
        public void EditUpdatesUpdatedAt()
        {
            var session = CreateSession(3);
            var service = new ConversationSummaryService();
            var summary = CreateSummary(session, 1, 2);
            service.AddSummary(session, summary);
            var before = summary.UpdatedAt;

            service.UpdateSummaryContent(session, summary.Id, "수정", "상황", "사건", "관계", "약속", "미해결", "상태");

            Assert.True(summary.UpdatedAt > before);
        }

        [Fact]
        public void EditDoesNotChangeRange()
        {
            var session = CreateSession(3);
            var service = new ConversationSummaryService();
            var summary = CreateSummary(session, 1, 2);
            service.AddSummary(session, summary);
            var start = summary.StartMessageId;
            var end = summary.EndMessageId;

            service.UpdateSummaryContent(session, summary.Id, "수정", "상황", "사건", "관계", "약속", "미해결", "상태");

            Assert.Equal(start, summary.StartMessageId);
            Assert.Equal(end, summary.EndMessageId);
        }

        private static ChatSession CreateSession(int count)
        {
            return new ChatSession
            {
                Messages = Enumerable.Range(1, count)
                    .Select(i => new ChatMessage(i % 2 == 0 ? ChatRole.Assistant : ChatRole.User, i.ToString()))
                    .ToList()
            };
        }

        private static ConversationSummary CreateSummary(ChatSession session, int startOneBased, int endOneBased)
        {
            return new ConversationSummary
            {
                StartMessageId = session.Messages[startOneBased - 1].Id,
                EndMessageId = session.Messages[endOneBased - 1].Id,
                Title = $"요약 {startOneBased}-{endOneBased}",
                CurrentSituation = "상황",
                KeyEvents = "사건",
                RelationshipChanges = "관계",
                PromisesAndImportantStatements = "약속",
                UnresolvedMatters = "미해결",
                PersistentState = "상태"
            };
        }
    }
}
