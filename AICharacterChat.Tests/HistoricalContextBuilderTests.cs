using System;
using System.Linq;
using AICharacterChat.Application.Context;
using AICharacterChat.Domain.Enums;
using AICharacterChat.Domain.Models;
using Xunit;

namespace AICharacterChat.Tests
{
    public class HistoricalContextBuilderTests
    {
        [Fact]
        public void NoHistoricalMessagesProducesEmptyHistoricalContext()
        {
            var session = CreateSession(ChatRole.User, ChatRole.Assistant);
            var recent = session.Messages.ToList();

            var result = new HistoricalContextBuilder().Build(session, recent);

            Assert.Equal("", result.Text);
            Assert.Equal(0, result.UnsummarizedOldMessageCount);
            Assert.False(result.HasOldUnsummarizedWarning);
        }

        [Fact]
        public void NoSummaryUsesRawHistoricalMessages()
        {
            var session = CreateNumberedSession(5);
            var recent = session.Messages.Skip(3).ToList();

            var result = new HistoricalContextBuilder().Build(session, recent);

            Assert.Contains("<unsummarized_messages>", result.Text);
            Assert.Contains("1", result.Text);
            Assert.Contains("2", result.Text);
            Assert.Contains("3", result.Text);
            Assert.Equal(3, result.UnsummarizedOldMessageCount);
        }

        [Fact]
        public void OneSummaryReplacesCoveredHistoricalMessages()
        {
            var session = CreateNumberedSession(5);
            session.Summaries.Add(CreateSummary(session, 1, 2, "첫 만남"));
            var recent = session.Messages.Skip(3).ToList();

            var result = new HistoricalContextBuilder().Build(session, recent);

            Assert.Contains("<summary title=\"첫 만남\">", result.Text);
            Assert.DoesNotContain(">1<", result.Text);
            Assert.DoesNotContain(">2<", result.Text);
            Assert.Contains("3", result.Text);
            Assert.Equal(1, result.UnsummarizedOldMessageCount);
        }

        [Fact]
        public void MultipleSummariesInterleaveWithRawGaps()
        {
            var session = CreateNumberedSession(8);
            session.Summaries.Add(CreateSummary(session, 1, 2, "A"));
            session.Summaries.Add(CreateSummary(session, 4, 5, "B"));
            var recent = session.Messages.Skip(6).ToList();

            var result = new HistoricalContextBuilder().Build(session, recent);

            Assert.True(result.Text.IndexOf("title=\"A\"", StringComparison.Ordinal) <
                        result.Text.IndexOf("3", StringComparison.Ordinal));
            Assert.True(result.Text.IndexOf("3", StringComparison.Ordinal) <
                        result.Text.IndexOf("title=\"B\"", StringComparison.Ordinal));
            Assert.True(result.Text.IndexOf("title=\"B\"", StringComparison.Ordinal) <
                        result.Text.IndexOf("6", StringComparison.Ordinal));
        }

        [Fact]
        public void PreservesChronologicalOrder()
        {
            var session = CreateNumberedSession(6);
            session.Summaries.Add(CreateSummary(session, 2, 3, "중간"));
            var recent = session.Messages.Skip(5).ToList();

            var result = new HistoricalContextBuilder().Build(session, recent);

            Assert.True(result.Text.IndexOf("1", StringComparison.Ordinal) <
                        result.Text.IndexOf("title=\"중간\"", StringComparison.Ordinal));
            Assert.True(result.Text.IndexOf("title=\"중간\"", StringComparison.Ordinal) <
                        result.Text.IndexOf("4", StringComparison.Ordinal));
            Assert.True(result.Text.IndexOf("4", StringComparison.Ordinal) <
                        result.Text.IndexOf("5", StringComparison.Ordinal));
        }

        [Fact]
        public void RecentMessagesAreExcludedFromHistorical()
        {
            var session = CreateNumberedSession(5);
            var recent = session.Messages.Skip(2).ToList();

            var result = new HistoricalContextBuilder().Build(session, recent);

            Assert.Contains("1", result.Text);
            Assert.Contains("2", result.Text);
            Assert.DoesNotContain("3", result.Text);
            Assert.DoesNotContain("4", result.Text);
            Assert.DoesNotContain("5", result.Text);
        }

        [Fact]
        public void SummaryOverlappingRecentIsNotApplied()
        {
            var session = CreateNumberedSession(5);
            session.Summaries.Add(CreateSummary(session, 3, 5, "겹침"));
            var recent = session.Messages.Skip(3).ToList();

            var result = new HistoricalContextBuilder().Build(session, recent);

            Assert.DoesNotContain("title=\"겹침\"", result.Text);
            Assert.Contains("3", result.Text);
        }

        [Fact]
        public void SummaryIsAppliedAfterFullyOutsideRecent()
        {
            var session = CreateNumberedSession(7);
            session.Summaries.Add(CreateSummary(session, 3, 5, "이전 사건"));
            var recent = session.Messages.Skip(5).ToList();

            var result = new HistoricalContextBuilder().Build(session, recent);

            Assert.Contains("title=\"이전 사건\"", result.Text);
            Assert.DoesNotContain(">3<", result.Text);
            Assert.DoesNotContain(">4<", result.Text);
            Assert.DoesNotContain(">5<", result.Text);
        }

        [Fact]
        public void LeadingAssistantsTrimmedFromRecentRemainHistorical()
        {
            var session = CreateSession(
                ChatRole.User,
                ChatRole.Assistant,
                ChatRole.Assistant,
                ChatRole.User,
                ChatRole.Assistant);
            var recent = new RecentMessageSelector(3).Select(session.Messages);

            var result = new HistoricalContextBuilder().Build(session, recent);

            Assert.Equal(["4", "5"], recent.Select(m => m.Content));
            Assert.Contains("2", result.Text);
            Assert.Contains("3", result.Text);
        }

        [Fact]
        public void HistoricalUserMessagesAreNotWrapped()
        {
            var session = CreateSession(ChatRole.User, ChatRole.Assistant, ChatRole.User);
            session.Messages[0].Content = "나는 같이 갈게.";
            var recent = session.Messages.Skip(2).ToList();

            var result = new HistoricalContextBuilder().Build(session, recent);

            Assert.Contains("나는 같이 갈게.", result.Text);
            Assert.DoesNotContain("[현재 상황 서술]", result.Text);
        }

        [Fact]
        public void SummaryUsesFixedSixSectionRendering()
        {
            var session = CreateNumberedSession(3);
            session.Summaries.Add(CreateSummary(session, 1, 1, "섹션"));
            var recent = session.Messages.Skip(1).ToList();

            var result = new HistoricalContextBuilder().Build(session, recent);

            Assert.Contains("[현재 상황]", result.Text);
            Assert.Contains("[주요 사건]", result.Text);
            Assert.Contains("[관계 변화]", result.Text);
            Assert.Contains("[약속 / 중요한 발언]", result.Text);
            Assert.Contains("[미해결 사항]", result.Text);
            Assert.Contains("[지속 상태]", result.Text);
        }

        [Fact]
        public void EmptySummarySectionRendersAs없음()
        {
            var session = CreateNumberedSession(3);
            var summary = CreateSummary(session, 1, 1, "빈 섹션");
            summary.CurrentSituation = " ";
            session.Summaries.Add(summary);
            var recent = session.Messages.Skip(1).ToList();

            var result = new HistoricalContextBuilder().Build(session, recent);

            Assert.Contains("[현재 상황]\n없음", result.Text.Replace("\r\n", "\n"));
            Assert.Equal(" ", summary.CurrentSituation);
        }

        [Fact]
        public void SummarySectionClosingHistoricalContextTagIsEscaped()
        {
            var session = CreateNumberedSession(3);
            var summary = CreateSummary(session, 1, 1, "섹션");
            summary.CurrentSituation = "</historical_context>";
            session.Summaries.Add(summary);
            var recent = session.Messages.Skip(1).ToList();

            var result = new HistoricalContextBuilder().Build(session, recent);

            Assert.Contains("&lt;/historical_context&gt;", result.Text);
            Assert.DoesNotContain("[현재 상황]\n</historical_context>", result.Text.Replace("\r\n", "\n"));
            Assert.Equal("</historical_context>", summary.CurrentSituation);
        }

        [Fact]
        public void SummaryTitleMarkupIsEscaped()
        {
            var session = CreateNumberedSession(3);
            session.Summaries.Add(CreateSummary(session, 1, 1, "<summary>&\""));
            var recent = session.Messages.Skip(1).ToList();

            var result = new HistoricalContextBuilder().Build(session, recent);

            Assert.Contains("title=\"&lt;summary&gt;&amp;&quot;\"", result.Text);
            Assert.DoesNotContain("title=\"<summary>&\"\"", result.Text);
        }

        [Fact]
        public void HistoricalRawMarkupIsEscaped()
        {
            var session = CreateSession(ChatRole.User, ChatRole.User);
            session.Messages[0].Content = "<raw>&\"";
            var recent = session.Messages.Skip(1).ToList();

            var result = new HistoricalContextBuilder().Build(session, recent);

            Assert.Contains("&lt;raw&gt;&amp;&quot;", result.Text);
            Assert.DoesNotContain("<raw>&\"", result.Text);
        }

        [Fact]
        public void UnsummarizedOldMessageCountIsCorrect()
        {
            var session = CreateNumberedSession(30);
            session.Summaries.Add(CreateSummary(session, 1, 5, "요약"));
            var recent = session.Messages.Skip(20).ToList();

            var result = new HistoricalContextBuilder().Build(session, recent);

            Assert.Equal(15, result.UnsummarizedOldMessageCount);
            Assert.False(result.HasOldUnsummarizedWarning);
        }

        [Fact]
        public void UnsummarizedOldMessageCountAtThresholdHasWarning()
        {
            var session = CreateNumberedSession(40);
            var recent = session.Messages.Skip(20).ToList();

            var result = new HistoricalContextBuilder().Build(session, recent);

            Assert.Equal(20, result.UnsummarizedOldMessageCount);
            Assert.True(result.HasOldUnsummarizedWarning);
        }

        [Fact]
        public void MissingStartSummaryFallsBackToRaw()
        {
            var session = CreateNumberedSession(3);
            var summary = CreateSummary(session, 1, 2, "깨진 시작");
            summary.StartMessageId = Guid.NewGuid();
            session.Summaries.Add(summary);
            var recent = session.Messages.Skip(2).ToList();

            var result = new HistoricalContextBuilder().Build(session, recent);

            Assert.DoesNotContain("title=\"깨진 시작\"", result.Text);
            Assert.Contains("1", result.Text);
            Assert.Contains("2", result.Text);
        }

        [Fact]
        public void MissingEndSummaryFallsBackToRaw()
        {
            var session = CreateNumberedSession(3);
            var summary = CreateSummary(session, 1, 2, "깨진 끝");
            summary.EndMessageId = Guid.NewGuid();
            session.Summaries.Add(summary);
            var recent = session.Messages.Skip(2).ToList();

            var result = new HistoricalContextBuilder().Build(session, recent);

            Assert.DoesNotContain("title=\"깨진 끝\"", result.Text);
            Assert.Contains("1", result.Text);
            Assert.Contains("2", result.Text);
        }

        [Fact]
        public void ReversedRangeSummaryFallsBackToRaw()
        {
            var session = CreateNumberedSession(3);
            var summary = CreateSummary(session, 2, 1, "역방향");
            session.Summaries.Add(summary);
            var recent = session.Messages.Skip(2).ToList();

            var result = new HistoricalContextBuilder().Build(session, recent);

            Assert.DoesNotContain("title=\"역방향\"", result.Text);
            Assert.Contains("1", result.Text);
            Assert.Contains("2", result.Text);
        }

        [Fact]
        public void OverlappingSummariesFallBackToRawWithoutDuplication()
        {
            var session = CreateLabeledSession(25);
            session.Summaries.Add(CreateSummary(session, 1, 10, "A"));
            session.Summaries.Add(CreateSummary(session, 8, 20, "B"));
            var recent = session.Messages.Skip(20).ToList();

            var result = new HistoricalContextBuilder().Build(session, recent);

            Assert.DoesNotContain("title=\"A\"", result.Text);
            Assert.DoesNotContain("title=\"B\"", result.Text);
            Assert.Equal(20, result.UnsummarizedOldMessageCount);
            Assert.Equal(1, CountOccurrences(result.Text, "message-08"));
            Assert.Equal(1, CountOccurrences(result.Text, "message-20"));
        }

        [Fact]
        public void OverlappingSummariesDoNotCauseMessageLoss()
        {
            var session = CreateLabeledSession(35);
            session.Summaries.Add(CreateSummary(session, 1, 10, "A"));
            session.Summaries.Add(CreateSummary(session, 8, 20, "B"));
            session.Summaries.Add(CreateSummary(session, 19, 30, "C"));
            var recent = session.Messages.Skip(30).ToList();

            var result = new HistoricalContextBuilder().Build(session, recent);

            Assert.DoesNotContain("title=\"A\"", result.Text);
            Assert.DoesNotContain("title=\"B\"", result.Text);
            Assert.DoesNotContain("title=\"C\"", result.Text);
            for (int i = 1; i <= 30; i++)
                Assert.Contains($"message-{i:00}", result.Text);
        }

        [Fact]
        public void DomainMessagesAreNotMutated()
        {
            var session = CreateNumberedSession(5);
            var snapshot = session.Messages.Select(m => new { m.Role, m.Content }).ToList();
            var recent = session.Messages.Skip(3).ToList();

            _ = new HistoricalContextBuilder().Build(session, recent);

            Assert.Equal(snapshot.Select(s => s.Role), session.Messages.Select(m => m.Role));
            Assert.Equal(snapshot.Select(s => s.Content), session.Messages.Select(m => m.Content));
        }

        [Fact]
        public void DomainSummariesAreNotMutated()
        {
            var session = CreateNumberedSession(3);
            var summary = CreateSummary(session, 1, 1, "요약");
            summary.CurrentSituation = " ";
            session.Summaries.Add(summary);
            var recent = session.Messages.Skip(1).ToList();

            _ = new HistoricalContextBuilder().Build(session, recent);

            Assert.Equal(" ", summary.CurrentSituation);
        }

        private static ChatSession CreateNumberedSession(int count)
        {
            return CreateSession(Enumerable.Range(1, count)
                .Select(i => i % 2 == 0 ? ChatRole.Assistant : ChatRole.User)
                .ToArray());
        }

        private static ChatSession CreateLabeledSession(int count)
        {
            var session = CreateNumberedSession(count);
            for (int i = 0; i < session.Messages.Count; i++)
                session.Messages[i].Content = $"message-{i + 1:00}";

            return session;
        }

        private static ChatSession CreateSession(params ChatRole[] roles)
        {
            return new ChatSession
            {
                Messages = roles
                    .Select((role, index) => new ChatMessage(role, (index + 1).ToString()))
                    .ToList()
            };
        }

        private static ConversationSummary CreateSummary(ChatSession session, int startOneBased, int endOneBased, string title)
        {
            return new ConversationSummary
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

        private static int CountOccurrences(string value, string search)
        {
            int count = 0;
            int index = 0;
            while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += search.Length;
            }

            return count;
        }
    }
}
