using System;
using System.Linq;
using System.Threading.Tasks;
using AICharacterChat.Application.Models;
using AICharacterChat.Application.Summaries;
using AICharacterChat.Domain.Enums;
using AICharacterChat.Domain.Models;
using Xunit;

namespace AICharacterChat.Tests
{
    public class ConversationSummaryPersistenceServiceTests
    {
        [Fact]
        public async Task AddSuccessPersistsSummary()
        {
            var (store, session) = CreateStore();
            var repository = new FakeWorldRepository();
            var service = CreateService(repository);

            var result = await service.AddAsync(store, session, CreateDraft(session, 1, 3));

            Assert.True(result.IsSuccess);
            Assert.Single(session.Summaries);
            Assert.Same(result.Summary, session.Summaries.Single());
            Assert.Equal(1, repository.SaveCallCount);
            Assert.Same(store, repository.LastSavedStore);
        }

        [Fact]
        public async Task AddSuccessReturnsCreatedSummary()
        {
            var (store, session) = CreateStore();
            var service = CreateService(new FakeWorldRepository());

            var result = await service.AddAsync(store, session, CreateDraft(session, 1, 2));

            Assert.NotNull(result.Summary);
            Assert.Same(result.Summary, session.Summaries.Single());
        }

        [Fact]
        public async Task AddUsesDraftRangeAndContent()
        {
            var (store, session) = CreateStore();
            var draft = CreateDraft(session, 2, 4);
            draft.Title = "  새 요약  ";
            draft.CurrentSituation = " ";
            draft.KeyEvents = "사건";
            var service = CreateService(new FakeWorldRepository());

            var result = await service.AddAsync(store, session, draft);

            Assert.True(result.IsSuccess);
            Assert.Equal(session.Messages[1].Id, result.Summary!.StartMessageId);
            Assert.Equal(session.Messages[3].Id, result.Summary.EndMessageId);
            Assert.Equal("새 요약", result.Summary.Title);
            Assert.Equal("없음", result.Summary.CurrentSituation);
            Assert.Equal("사건", result.Summary.KeyEvents);
        }

        [Fact]
        public async Task AddValidationFailureDoesNotCallRepository()
        {
            var (store, session) = CreateStore();
            var repository = new FakeWorldRepository();
            var draft = CreateDraft(session, 1, 2);
            draft.Title = " ";

            var result = await CreateService(repository).AddAsync(store, session, draft);

            Assert.False(result.IsSuccess);
            Assert.Empty(session.Summaries);
            Assert.Equal(0, repository.SaveCallCount);
        }

        [Fact]
        public async Task AddOverlapDoesNotCallRepository()
        {
            var (store, session) = CreateStore();
            var existing = CreateSummary(session, 1, 3, "기존");
            session.Summaries.Add(existing);
            var repository = new FakeWorldRepository();

            var result = await CreateService(repository).AddAsync(store, session, CreateDraft(session, 2, 4));

            Assert.False(result.IsSuccess);
            Assert.Single(session.Summaries);
            Assert.Same(existing, session.Summaries.Single());
            Assert.Equal(0, repository.SaveCallCount);
        }

        [Fact]
        public async Task AddSaveFailureRemovesAddedSummary()
        {
            var (store, session) = CreateStore();
            var first = CreateSummary(session, 1, 1, "첫번째");
            var last = CreateSummary(session, 5, 5, "마지막");
            session.Summaries.Add(first);
            session.Summaries.Add(last);
            var repository = new FakeWorldRepository { SaveException = new InvalidOperationException("저장 실패") };

            var result = await CreateService(repository).AddAsync(store, session, CreateDraft(session, 2, 3));

            Assert.False(result.IsSuccess);
            Assert.Equal(1, repository.SaveCallCount);
            Assert.Equal(2, session.Summaries.Count);
            Assert.Same(first, session.Summaries[0]);
            Assert.Same(last, session.Summaries[1]);
        }

        [Fact]
        public async Task AddSaveCancellationRollsBack()
        {
            var (store, session) = CreateStore();
            var existing = CreateSummary(session, 1, 1, "기존");
            session.Summaries.Add(existing);
            var repository = new FakeWorldRepository { CancelSave = true };

            var result = await CreateService(repository).AddAsync(store, session, CreateDraft(session, 2, 3));

            Assert.True(result.IsCanceled);
            Assert.Equal(1, repository.SaveCallCount);
            Assert.Single(session.Summaries);
            Assert.Same(existing, session.Summaries.Single());
        }

        [Fact]
        public async Task UpdateSuccessPersistsChanges()
        {
            var (store, session) = CreateStore();
            var target = CreateSummary(session, 1, 3, "기존");
            session.Summaries.Add(target);
            var repository = new FakeWorldRepository();

            var result = await CreateService(repository).UpdateAsync(
                store,
                session,
                target,
                CreateDraft(session, 1, 3, "수정"));

            Assert.True(result.IsSuccess);
            Assert.Equal("수정", target.Title);
            Assert.Equal(1, repository.SaveCallCount);
        }

        [Fact]
        public async Task UpdateSuccessReturnsSameTarget()
        {
            var (store, session) = CreateStore();
            var target = CreateSummary(session, 1, 3, "기존");
            session.Summaries.Add(target);

            var result = await CreateService(new FakeWorldRepository()).UpdateAsync(
                store,
                session,
                target,
                CreateDraft(session, 1, 3, "수정"));

            Assert.Same(target, result.Summary);
        }

        [Fact]
        public async Task UpdateIncrementsRevision()
        {
            var (store, session) = CreateStore();
            var target = CreateSummary(session, 1, 3, "기존");
            target.Revision = 7;
            session.Summaries.Add(target);

            await CreateService(new FakeWorldRepository()).UpdateAsync(
                store,
                session,
                target,
                CreateDraft(session, 1, 3, "수정"));

            Assert.Equal(8, target.Revision);
        }

        [Fact]
        public async Task UpdateChangesUpdatedAt()
        {
            var (store, session) = CreateStore();
            var target = CreateSummary(session, 1, 3, "기존");
            var oldUpdatedAt = DateTimeOffset.Now.AddDays(-1);
            target.UpdatedAt = oldUpdatedAt;
            session.Summaries.Add(target);

            await CreateService(new FakeWorldRepository()).UpdateAsync(
                store,
                session,
                target,
                CreateDraft(session, 1, 3, "수정"));

            Assert.True(target.UpdatedAt > oldUpdatedAt);
        }

        [Fact]
        public async Task UpdateKeepsId()
        {
            var (store, session) = CreateStore();
            var target = CreateSummary(session, 1, 3, "기존");
            var id = target.Id;
            session.Summaries.Add(target);

            await CreateService(new FakeWorldRepository()).UpdateAsync(
                store,
                session,
                target,
                CreateDraft(session, 1, 3, "수정"));

            Assert.Equal(id, target.Id);
        }

        [Fact]
        public async Task UpdateKeepsCreatedAt()
        {
            var (store, session) = CreateStore();
            var target = CreateSummary(session, 1, 3, "기존");
            var createdAt = DateTimeOffset.Now.AddDays(-10);
            target.CreatedAt = createdAt;
            session.Summaries.Add(target);

            await CreateService(new FakeWorldRepository()).UpdateAsync(
                store,
                session,
                target,
                CreateDraft(session, 1, 3, "수정"));

            Assert.Equal(createdAt, target.CreatedAt);
        }

        [Fact]
        public async Task UpdateKeepsRange()
        {
            var (store, session) = CreateStore();
            var target = CreateSummary(session, 1, 3, "기존");
            var start = target.StartMessageId;
            var end = target.EndMessageId;
            session.Summaries.Add(target);

            await CreateService(new FakeWorldRepository()).UpdateAsync(
                store,
                session,
                target,
                CreateDraft(session, 1, 3, "수정"));

            Assert.Equal(start, target.StartMessageId);
            Assert.Equal(end, target.EndMessageId);
        }

        [Fact]
        public async Task UpdateSaveFailureRestoresContent()
        {
            var (store, session) = CreateStore();
            var target = CreateSummary(session, 1, 3, "기존");
            session.Summaries.Add(target);
            var repository = new FakeWorldRepository { SaveException = new InvalidOperationException("저장 실패") };

            var result = await CreateService(repository).UpdateAsync(
                store,
                session,
                target,
                CreateDraft(session, 1, 3, "수정"));

            Assert.False(result.IsSuccess);
            Assert.Equal("기존", target.Title);
            Assert.Equal("상황", target.CurrentSituation);
            Assert.Equal("사건", target.KeyEvents);
            Assert.Equal(1, repository.SaveCallCount);
        }

        [Fact]
        public async Task UpdateSaveFailureRestoresRevision()
        {
            var (store, session) = CreateStore();
            var target = CreateSummary(session, 1, 3, "기존");
            target.Revision = 4;
            session.Summaries.Add(target);
            var repository = new FakeWorldRepository { SaveException = new InvalidOperationException("저장 실패") };

            await CreateService(repository).UpdateAsync(store, session, target, CreateDraft(session, 1, 3, "수정"));

            Assert.Equal(4, target.Revision);
        }

        [Fact]
        public async Task UpdateSaveFailureRestoresUpdatedAt()
        {
            var (store, session) = CreateStore();
            var target = CreateSummary(session, 1, 3, "기존");
            var updatedAt = DateTimeOffset.Now.AddDays(-5);
            target.UpdatedAt = updatedAt;
            session.Summaries.Add(target);
            var repository = new FakeWorldRepository { SaveException = new InvalidOperationException("저장 실패") };

            await CreateService(repository).UpdateAsync(store, session, target, CreateDraft(session, 1, 3, "수정"));

            Assert.Equal(updatedAt, target.UpdatedAt);
        }

        [Fact]
        public async Task UpdateSaveFailurePreservesObjectIdentity()
        {
            var (store, session) = CreateStore();
            var target = CreateSummary(session, 1, 3, "기존");
            session.Summaries.Add(target);
            var repository = new FakeWorldRepository { SaveException = new InvalidOperationException("저장 실패") };

            await CreateService(repository).UpdateAsync(store, session, target, CreateDraft(session, 1, 3, "수정"));

            Assert.Same(target, session.Summaries.Single());
        }

        [Fact]
        public async Task UpdateSaveCancellationRestoresState()
        {
            var (store, session) = CreateStore();
            var target = CreateSummary(session, 1, 3, "기존");
            target.Revision = 2;
            var updatedAt = DateTimeOffset.Now.AddDays(-3);
            target.UpdatedAt = updatedAt;
            session.Summaries.Add(target);
            var repository = new FakeWorldRepository { CancelSave = true };

            var result = await CreateService(repository).UpdateAsync(
                store,
                session,
                target,
                CreateDraft(session, 1, 3, "수정"));

            Assert.True(result.IsCanceled);
            Assert.Equal("기존", target.Title);
            Assert.Equal(2, target.Revision);
            Assert.Equal(updatedAt, target.UpdatedAt);
            Assert.Same(target, session.Summaries.Single());
        }

        [Fact]
        public async Task UpdateRangeMismatchFailsBeforeMutation()
        {
            var (store, session) = CreateStore();
            var target = CreateSummary(session, 1, 3, "기존");
            session.Summaries.Add(target);
            var repository = new FakeWorldRepository();

            var result = await CreateService(repository).UpdateAsync(
                store,
                session,
                target,
                CreateDraft(session, 2, 3, "수정"));

            Assert.False(result.IsSuccess);
            Assert.Equal("기존", target.Title);
            Assert.Equal(0, repository.SaveCallCount);
        }

        [Fact]
        public async Task UpdateTargetNotInSessionFails()
        {
            var (store, session) = CreateStore();
            var target = CreateSummary(session, 1, 3, "외부");
            var repository = new FakeWorldRepository();

            var result = await CreateService(repository).UpdateAsync(
                store,
                session,
                target,
                CreateDraft(session, 1, 3, "수정"));

            Assert.False(result.IsSuccess);
            Assert.Equal("외부", target.Title);
            Assert.Equal(0, repository.SaveCallCount);
        }

        [Fact]
        public async Task DeleteSuccessRemovesTarget()
        {
            var (store, session) = CreateStore();
            var target = CreateSummary(session, 1, 3, "기존");
            session.Summaries.Add(target);

            var result = await CreateService(new FakeWorldRepository()).DeleteAsync(store, session, target);

            Assert.True(result.IsSuccess);
            Assert.Empty(session.Summaries);
        }

        [Fact]
        public async Task DeleteSuccessReturnsRemovedTarget()
        {
            var (store, session) = CreateStore();
            var target = CreateSummary(session, 1, 3, "기존");
            session.Summaries.Add(target);

            var result = await CreateService(new FakeWorldRepository()).DeleteAsync(store, session, target);

            Assert.Same(target, result.Summary);
        }

        [Fact]
        public async Task DeleteSuccessCallsRepositoryOnce()
        {
            var (store, session) = CreateStore();
            var target = CreateSummary(session, 1, 3, "기존");
            session.Summaries.Add(target);
            var repository = new FakeWorldRepository();

            await CreateService(repository).DeleteAsync(store, session, target);

            Assert.Equal(1, repository.SaveCallCount);
        }

        [Fact]
        public async Task DeleteSaveFailureRestoresTarget()
        {
            var (store, session) = CreateStore();
            var target = CreateSummary(session, 2, 3, "대상");
            session.Summaries.Add(CreateSummary(session, 1, 1, "앞"));
            session.Summaries.Add(target);
            var repository = new FakeWorldRepository { SaveException = new InvalidOperationException("저장 실패") };

            var result = await CreateService(repository).DeleteAsync(store, session, target);

            Assert.False(result.IsSuccess);
            Assert.Contains(session.Summaries, summary => ReferenceEquals(summary, target));
        }

        [Fact]
        public async Task DeleteSaveFailureRestoresOriginalIndex()
        {
            var (store, session) = CreateStore();
            var first = CreateSummary(session, 1, 1, "앞");
            var target = CreateSummary(session, 2, 3, "대상");
            var last = CreateSummary(session, 5, 5, "뒤");
            session.Summaries.Add(first);
            session.Summaries.Add(target);
            session.Summaries.Add(last);
            var repository = new FakeWorldRepository { SaveException = new InvalidOperationException("저장 실패") };

            await CreateService(repository).DeleteAsync(store, session, target);

            Assert.Same(first, session.Summaries[0]);
            Assert.Same(target, session.Summaries[1]);
            Assert.Same(last, session.Summaries[2]);
        }

        [Fact]
        public async Task DeleteSaveFailurePreservesObjectIdentity()
        {
            var (store, session) = CreateStore();
            var target = CreateSummary(session, 1, 3, "대상");
            session.Summaries.Add(target);
            var repository = new FakeWorldRepository { SaveException = new InvalidOperationException("저장 실패") };

            await CreateService(repository).DeleteAsync(store, session, target);

            Assert.Same(target, session.Summaries.Single());
        }

        [Fact]
        public async Task DeleteSaveCancellationRestoresTarget()
        {
            var (store, session) = CreateStore();
            var target = CreateSummary(session, 1, 3, "대상");
            session.Summaries.Add(target);
            var repository = new FakeWorldRepository { CancelSave = true };

            var result = await CreateService(repository).DeleteAsync(store, session, target);

            Assert.True(result.IsCanceled);
            Assert.Single(session.Summaries);
            Assert.Same(target, session.Summaries.Single());
        }

        [Fact]
        public async Task SessionNotInStoreFailsForAdd()
        {
            var (store, _) = CreateStore();
            var externalSession = CreateSession();
            var repository = new FakeWorldRepository();

            var result = await CreateService(repository).AddAsync(
                store,
                externalSession,
                CreateDraft(externalSession, 1, 2));

            Assert.False(result.IsSuccess);
            Assert.Empty(externalSession.Summaries);
            Assert.Equal(0, repository.SaveCallCount);
        }

        [Fact]
        public async Task SessionNotInStoreFailsForUpdate()
        {
            var (store, _) = CreateStore();
            var externalSession = CreateSession();
            var target = CreateSummary(externalSession, 1, 2, "외부");
            externalSession.Summaries.Add(target);
            var repository = new FakeWorldRepository();

            var result = await CreateService(repository).UpdateAsync(
                store,
                externalSession,
                target,
                CreateDraft(externalSession, 1, 2, "수정"));

            Assert.False(result.IsSuccess);
            Assert.Equal("외부", target.Title);
            Assert.Equal(0, repository.SaveCallCount);
        }

        [Fact]
        public async Task SessionNotInStoreFailsForDelete()
        {
            var (store, _) = CreateStore();
            var externalSession = CreateSession();
            var target = CreateSummary(externalSession, 1, 2, "외부");
            externalSession.Summaries.Add(target);
            var repository = new FakeWorldRepository();

            var result = await CreateService(repository).DeleteAsync(store, externalSession, target);

            Assert.False(result.IsSuccess);
            Assert.Single(externalSession.Summaries);
            Assert.Equal(0, repository.SaveCallCount);
        }

        private static ConversationSummaryPersistenceService CreateService(FakeWorldRepository repository) =>
            new(new ConversationSummaryService(), repository);

        private static (WorldStore Store, ChatSession Session) CreateStore()
        {
            var session = CreateSession();
            var world = new World
            {
                Id = "world-1",
                Name = "세계",
                ChatSessions = [session]
            };
            session.WorldId = world.Id;

            return (new WorldStore
            {
                ActiveWorldId = world.Id,
                Worlds = [world]
            }, session);
        }

        private static ChatSession CreateSession() =>
            new()
            {
                Id = Guid.NewGuid().ToString(),
                Messages = Enumerable.Range(1, 5)
                    .Select(i => new ChatMessage(
                        i % 2 == 0 ? ChatRole.Assistant : ChatRole.User,
                        $"message-{i}"))
                    .ToList()
            };

        private static SummaryDraft CreateDraft(
            ChatSession session,
            int startOneBased,
            int endOneBased,
            string title = "초안") =>
            new()
            {
                StartMessageId = session.Messages[startOneBased - 1].Id,
                EndMessageId = session.Messages[endOneBased - 1].Id,
                Title = title,
                CurrentSituation = "새 상황",
                KeyEvents = "새 사건",
                RelationshipChanges = "새 관계",
                PromisesAndImportantStatements = "새 약속",
                UnresolvedMatters = "새 미해결",
                PersistentState = "새 상태"
            };

        private static ConversationSummary CreateSummary(
            ChatSession session,
            int startOneBased,
            int endOneBased,
            string title) =>
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
