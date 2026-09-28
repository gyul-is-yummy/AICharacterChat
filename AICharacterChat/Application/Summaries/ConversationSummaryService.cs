using System;
using System.Linq;
using AICharacterChat.Application.Models;
using AICharacterChat.Domain.Models;

namespace AICharacterChat.Application.Summaries
{
    public class ConversationSummaryService
    {
        public const int MaxTitleLength = 40;

        public SummaryServiceResult AddSummary(ChatSession session, ConversationSummary summary)
        {
            var validation = ValidateSummary(session, summary);
            if (!validation.IsSuccess)
                return validation;

            var now = DateTimeOffset.Now;
            if (summary.CreatedAt == default)
                summary.CreatedAt = now;
            if (summary.UpdatedAt == default)
                summary.UpdatedAt = summary.CreatedAt;
            if (summary.Revision <= 0)
                summary.Revision = 1;

            NormalizeSections(summary);
            session.Summaries.Add(summary);
            return SummaryServiceResult.Success(summary);
        }

        public SummaryServiceResult UpdateSummaryContent(
            ChatSession session,
            Guid summaryId,
            string title,
            string currentSituation,
            string keyEvents,
            string relationshipChanges,
            string promisesAndImportantStatements,
            string unresolvedMatters,
            string persistentState)
        {
            var summary = session.Summaries.FirstOrDefault(s => s.Id == summaryId);
            if (summary == null)
                return SummaryServiceResult.Failure("수정할 요약을 찾을 수 없습니다.");

            var titleValidation = ValidateTitle(title);
            if (!titleValidation.IsSuccess)
                return titleValidation;

            summary.Title = title.Trim();
            summary.CurrentSituation = NormalizeSection(currentSituation);
            summary.KeyEvents = NormalizeSection(keyEvents);
            summary.RelationshipChanges = NormalizeSection(relationshipChanges);
            summary.PromisesAndImportantStatements = NormalizeSection(promisesAndImportantStatements);
            summary.UnresolvedMatters = NormalizeSection(unresolvedMatters);
            summary.PersistentState = NormalizeSection(persistentState);
            summary.UpdatedAt = DateTimeOffset.Now;
            summary.Revision += 1;

            return SummaryServiceResult.Success(summary);
        }

        public SummaryServiceResult DeleteSummary(ChatSession session, Guid summaryId)
        {
            var summary = session.Summaries.FirstOrDefault(s => s.Id == summaryId);
            if (summary == null)
                return SummaryServiceResult.Failure("삭제할 요약을 찾을 수 없습니다.");

            session.Summaries.Remove(summary);
            return SummaryServiceResult.Success(summary);
        }

        public SummaryServiceResult ValidateRange(
            ChatSession session,
            Guid startMessageId,
            Guid endMessageId,
            Guid? ignoreSummaryId = null)
        {
            int startIndex = session.Messages.FindIndex(m => m.Id == startMessageId);
            if (startIndex < 0)
                return SummaryServiceResult.Failure("시작 메시지를 찾을 수 없습니다.");

            int endIndex = session.Messages.FindIndex(m => m.Id == endMessageId);
            if (endIndex < 0)
                return SummaryServiceResult.Failure("끝 메시지를 찾을 수 없습니다.");

            if (startIndex > endIndex)
                return SummaryServiceResult.Failure("시작 메시지는 끝 메시지보다 앞에 있어야 합니다.");

            foreach (var existing in session.Summaries.Where(s => s.Id != ignoreSummaryId))
            {
                int existingStart = session.Messages.FindIndex(m => m.Id == existing.StartMessageId);
                int existingEnd = session.Messages.FindIndex(m => m.Id == existing.EndMessageId);
                if (existingStart < 0 || existingEnd < 0)
                    continue;

                if (startIndex <= existingEnd && existingStart <= endIndex)
                    return SummaryServiceResult.Failure("이미 저장된 요약 범위와 겹칩니다.");
            }

            return SummaryServiceResult.Success();
        }

        private SummaryServiceResult ValidateSummary(ChatSession session, ConversationSummary summary)
        {
            var titleValidation = ValidateTitle(summary.Title);
            if (!titleValidation.IsSuccess)
                return titleValidation;

            return ValidateRange(session, summary.StartMessageId, summary.EndMessageId, summary.Id);
        }

        private static SummaryServiceResult ValidateTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return SummaryServiceResult.Failure("요약 제목을 입력해주세요.");

            if (title.Trim().Length > MaxTitleLength)
                return SummaryServiceResult.Failure($"요약 제목은 {MaxTitleLength}자 이하여야 합니다.");

            return SummaryServiceResult.Success();
        }

        private static void NormalizeSections(ConversationSummary summary)
        {
            summary.Title = summary.Title.Trim();
            summary.CurrentSituation = NormalizeSection(summary.CurrentSituation);
            summary.KeyEvents = NormalizeSection(summary.KeyEvents);
            summary.RelationshipChanges = NormalizeSection(summary.RelationshipChanges);
            summary.PromisesAndImportantStatements = NormalizeSection(summary.PromisesAndImportantStatements);
            summary.UnresolvedMatters = NormalizeSection(summary.UnresolvedMatters);
            summary.PersistentState = NormalizeSection(summary.PersistentState);
        }

        private static string NormalizeSection(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "없음" : value.Trim();
    }
}
