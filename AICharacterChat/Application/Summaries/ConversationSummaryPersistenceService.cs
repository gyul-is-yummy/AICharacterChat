using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AICharacterChat.Application.Interfaces;
using AICharacterChat.Application.Models;
using AICharacterChat.Domain.Models;

namespace AICharacterChat.Application.Summaries
{
    public sealed class ConversationSummaryPersistenceService
    {
        private readonly ConversationSummaryService _summaryService;
        private readonly IWorldRepository _worldRepository;

        public ConversationSummaryPersistenceService(
            ConversationSummaryService summaryService,
            IWorldRepository worldRepository)
        {
            _summaryService = summaryService;
            _worldRepository = worldRepository;
        }

        public async Task<SummaryPersistenceResult> AddAsync(
            WorldStore store,
            ChatSession session,
            SummaryDraft draft,
            CancellationToken cancellationToken = default)
        {
            if (!ContainsSessionReference(store, session))
                return SummaryPersistenceResult.Failure("대화 세션을 찾을 수 없습니다.");

            var summary = CreateSummary(draft);
            var addResult = _summaryService.AddSummary(session, summary);
            if (!addResult.IsSuccess)
                return SummaryPersistenceResult.Failure(addResult.ErrorMessage ?? "요약을 저장할 수 없습니다.");

            try
            {
                await _worldRepository.SaveAsync(store, cancellationToken);
                return SummaryPersistenceResult.Success(summary);
            }
            catch (OperationCanceledException)
            {
                RemoveSummaryReference(session, summary);
                return SummaryPersistenceResult.Canceled();
            }
            catch (Exception)
            {
                RemoveSummaryReference(session, summary);
                return SummaryPersistenceResult.Failure("요약 저장에 실패했습니다.");
            }
        }

        public async Task<SummaryPersistenceResult> UpdateAsync(
            WorldStore store,
            ChatSession session,
            ConversationSummary target,
            SummaryDraft draft,
            CancellationToken cancellationToken = default)
        {
            if (!ContainsSessionReference(store, session))
                return SummaryPersistenceResult.Failure("대화 세션을 찾을 수 없습니다.");

            if (!ContainsSummaryReference(session, target))
                return SummaryPersistenceResult.Failure("저장할 요약이 현재 세션에 존재하지 않습니다.");

            if (target.StartMessageId != draft.StartMessageId || target.EndMessageId != draft.EndMessageId)
                return SummaryPersistenceResult.Failure("요약 범위는 변경할 수 없습니다.");

            var snapshot = SummaryStateSnapshot.Capture(target);
            var updateResult = _summaryService.UpdateSummaryContent(
                session,
                target,
                draft.Title,
                draft.CurrentSituation,
                draft.KeyEvents,
                draft.RelationshipChanges,
                draft.PromisesAndImportantStatements,
                draft.UnresolvedMatters,
                draft.PersistentState);
            if (!updateResult.IsSuccess)
                return SummaryPersistenceResult.Failure(updateResult.ErrorMessage ?? "요약을 수정할 수 없습니다.");

            try
            {
                await _worldRepository.SaveAsync(store, cancellationToken);
                return SummaryPersistenceResult.Success(target);
            }
            catch (OperationCanceledException)
            {
                snapshot.Restore(target);
                return SummaryPersistenceResult.Canceled();
            }
            catch (Exception)
            {
                snapshot.Restore(target);
                return SummaryPersistenceResult.Failure("요약 저장에 실패했습니다.");
            }
        }

        public async Task<SummaryPersistenceResult> DeleteAsync(
            WorldStore store,
            ChatSession session,
            ConversationSummary target,
            CancellationToken cancellationToken = default)
        {
            if (!ContainsSessionReference(store, session))
                return SummaryPersistenceResult.Failure("대화 세션을 찾을 수 없습니다.");

            int originalIndex = FindSummaryReferenceIndex(session, target);
            if (originalIndex < 0)
                return SummaryPersistenceResult.Failure("저장할 요약이 현재 세션에 존재하지 않습니다.");

            var deleteResult = _summaryService.DeleteSummary(session, target);
            if (!deleteResult.IsSuccess)
                return SummaryPersistenceResult.Failure(deleteResult.ErrorMessage ?? "요약을 삭제할 수 없습니다.");

            try
            {
                await _worldRepository.SaveAsync(store, cancellationToken);
                return SummaryPersistenceResult.Success(target);
            }
            catch (OperationCanceledException)
            {
                session.Summaries.Insert(originalIndex, target);
                return SummaryPersistenceResult.Canceled();
            }
            catch (Exception)
            {
                session.Summaries.Insert(originalIndex, target);
                return SummaryPersistenceResult.Failure("요약 저장에 실패했습니다.");
            }
        }

        private static ConversationSummary CreateSummary(SummaryDraft draft) =>
            new()
            {
                StartMessageId = draft.StartMessageId,
                EndMessageId = draft.EndMessageId,
                Title = draft.Title,
                CurrentSituation = draft.CurrentSituation,
                KeyEvents = draft.KeyEvents,
                RelationshipChanges = draft.RelationshipChanges,
                PromisesAndImportantStatements = draft.PromisesAndImportantStatements,
                UnresolvedMatters = draft.UnresolvedMatters,
                PersistentState = draft.PersistentState
            };

        private static bool ContainsSessionReference(WorldStore store, ChatSession session) =>
            store.Worlds.Any(world => world.ChatSessions.Any(candidate => ReferenceEquals(candidate, session)));

        private static bool ContainsSummaryReference(ChatSession session, ConversationSummary target) =>
            FindSummaryReferenceIndex(session, target) >= 0;

        private static int FindSummaryReferenceIndex(ChatSession session, ConversationSummary target)
        {
            for (int i = 0; i < session.Summaries.Count; i++)
            {
                if (ReferenceEquals(session.Summaries[i], target))
                    return i;
            }

            return -1;
        }

        private static void RemoveSummaryReference(ChatSession session, ConversationSummary target)
        {
            int index = FindSummaryReferenceIndex(session, target);
            if (index >= 0)
                session.Summaries.RemoveAt(index);
        }

        private sealed record SummaryStateSnapshot(
            string Title,
            string CurrentSituation,
            string KeyEvents,
            string RelationshipChanges,
            string PromisesAndImportantStatements,
            string UnresolvedMatters,
            string PersistentState,
            DateTimeOffset UpdatedAt,
            int Revision)
        {
            public static SummaryStateSnapshot Capture(ConversationSummary summary) =>
                new(
                    summary.Title,
                    summary.CurrentSituation,
                    summary.KeyEvents,
                    summary.RelationshipChanges,
                    summary.PromisesAndImportantStatements,
                    summary.UnresolvedMatters,
                    summary.PersistentState,
                    summary.UpdatedAt,
                    summary.Revision);

            public void Restore(ConversationSummary summary)
            {
                summary.Title = Title;
                summary.CurrentSituation = CurrentSituation;
                summary.KeyEvents = KeyEvents;
                summary.RelationshipChanges = RelationshipChanges;
                summary.PromisesAndImportantStatements = PromisesAndImportantStatements;
                summary.UnresolvedMatters = UnresolvedMatters;
                summary.PersistentState = PersistentState;
                summary.UpdatedAt = UpdatedAt;
                summary.Revision = Revision;
            }
        }
    }
}
