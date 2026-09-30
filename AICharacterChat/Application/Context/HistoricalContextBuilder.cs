using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using AICharacterChat.Application.Models;
using AICharacterChat.Domain.Enums;
using AICharacterChat.Domain.Models;

namespace AICharacterChat.Application.Context
{
    public class HistoricalContextBuilder
    {
        public BuiltHistoricalContext Build(
            ChatSession session,
            IReadOnlyList<ChatMessage> recentMessages)
        {
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(recentMessages);

            int historicalEndExclusive = GetHistoricalEndExclusive(session.Messages, recentMessages);
            if (historicalEndExclusive <= 0)
                return new BuiltHistoricalContext();

            var summaries = GetApplicableSummaries(session, historicalEndExclusive);
            var builder = new StringBuilder();
            int cursor = 0;
            int unsummarizedCount = 0;

            builder.AppendLine("<historical_context>");

            foreach (var summaryRange in summaries)
            {
                if (summaryRange.StartIndex > cursor)
                {
                    AppendRawBlock(builder, session.Messages, cursor, summaryRange.StartIndex - 1);
                    unsummarizedCount += summaryRange.StartIndex - cursor;
                }

                AppendSummary(builder, summaryRange.Summary);
                cursor = summaryRange.EndIndex + 1;
            }

            if (cursor < historicalEndExclusive)
            {
                AppendRawBlock(builder, session.Messages, cursor, historicalEndExclusive - 1);
                unsummarizedCount += historicalEndExclusive - cursor;
            }

            builder.AppendLine("</historical_context>");

            return new BuiltHistoricalContext
            {
                Text = builder.ToString().Trim(),
                UnsummarizedOldMessageCount = unsummarizedCount
            };
        }

        private static int GetHistoricalEndExclusive(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ChatMessage> recentMessages)
        {
            if (messages.Count == 0)
                return 0;

            if (recentMessages.Count == 0)
                return messages.Count;

            var firstRecentId = recentMessages[0].Id;
            int firstRecentIndex = FindMessageIndex(messages, firstRecentId);
            return firstRecentIndex < 0 ? messages.Count : firstRecentIndex;
        }

        private static List<SummaryRange> GetApplicableSummaries(
            ChatSession session,
            int historicalEndExclusive)
        {
            var ranges = session.Summaries
                .Select(summary => CreateSummaryRange(session.Messages, summary))
                .Where(range => range != null)
                .Cast<SummaryRange>()
                .Where(range => range.StartIndex >= 0 &&
                                range.EndIndex < historicalEndExclusive &&
                                range.StartIndex <= range.EndIndex)
                .OrderBy(range => range.StartIndex)
                .ToList();

            if (ranges.Count <= 1)
                return ranges;

            var conflicting = new bool[ranges.Count];
            for (int i = 0; i < ranges.Count; i++)
            {
                for (int j = i + 1; j < ranges.Count; j++)
                {
                    if (Overlaps(ranges[i], ranges[j]))
                    {
                        conflicting[i] = true;
                        conflicting[j] = true;
                    }
                }
            }

            return ranges
                .Where((_, index) => !conflicting[index])
                .ToList();
        }

        private static SummaryRange? CreateSummaryRange(
            IReadOnlyList<ChatMessage> messages,
            ConversationSummary summary)
        {
            int startIndex = FindMessageIndex(messages, summary.StartMessageId);
            int endIndex = FindMessageIndex(messages, summary.EndMessageId);
            if (startIndex < 0 || endIndex < 0)
                return null;

            return new SummaryRange(summary, startIndex, endIndex);
        }

        private static int FindMessageIndex(IReadOnlyList<ChatMessage> messages, Guid messageId)
        {
            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i].Id == messageId)
                    return i;
            }

            return -1;
        }

        private static void AppendSummary(StringBuilder builder, ConversationSummary summary)
        {
            builder.AppendLine();
            builder.AppendLine($"<summary title=\"{Escape(summary.Title)}\">");
            builder.AppendLine("[현재 상황]");
            builder.AppendLine(Escape(RenderSection(summary.CurrentSituation)));
            builder.AppendLine();
            builder.AppendLine("[주요 사건]");
            builder.AppendLine(Escape(RenderSection(summary.KeyEvents)));
            builder.AppendLine();
            builder.AppendLine("[관계 변화]");
            builder.AppendLine(Escape(RenderSection(summary.RelationshipChanges)));
            builder.AppendLine();
            builder.AppendLine("[약속 / 중요한 발언]");
            builder.AppendLine(Escape(RenderSection(summary.PromisesAndImportantStatements)));
            builder.AppendLine();
            builder.AppendLine("[미해결 사항]");
            builder.AppendLine(Escape(RenderSection(summary.UnresolvedMatters)));
            builder.AppendLine();
            builder.AppendLine("[지속 상태]");
            builder.AppendLine(Escape(RenderSection(summary.PersistentState)));
            builder.AppendLine("</summary>");
        }

        private static void AppendRawBlock(
            StringBuilder builder,
            IReadOnlyList<ChatMessage> messages,
            int startIndex,
            int endIndex)
        {
            builder.AppendLine();
            builder.AppendLine("<unsummarized_messages>");
            for (int i = startIndex; i <= endIndex; i++)
            {
                builder.AppendLine($"  <message role=\"{ToRoleText(messages[i].Role)}\">");
                builder.AppendLine($"  {Escape(messages[i].Content)}");
                builder.AppendLine("  </message>");
                if (i < endIndex)
                    builder.AppendLine();
            }

            builder.AppendLine("</unsummarized_messages>");
        }

        private static string ToRoleText(ChatRole role) =>
            role == ChatRole.User ? "user" : "assistant";

        private static string RenderSection(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "없음" : value.Trim();

        private static string Escape(string? value) =>
            WebUtility.HtmlEncode(value ?? "");

        private static bool Overlaps(SummaryRange left, SummaryRange right) =>
            left.StartIndex <= right.EndIndex &&
            right.StartIndex <= left.EndIndex;

        private sealed record SummaryRange(
            ConversationSummary Summary,
            int StartIndex,
            int EndIndex);
    }
}
