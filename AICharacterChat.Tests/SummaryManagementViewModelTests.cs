using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AICharacterChat.Application.Interfaces;
using AICharacterChat.Application.Models;
using AICharacterChat.Application.Summaries;
using AICharacterChat.Domain.Enums;
using AICharacterChat.Domain.Models;
using AICharacterChat.Presentation.Models;
using AICharacterChat.Presentation.ViewModels;
using Xunit;

namespace AICharacterChat.Tests
{
    public class SummaryManagementViewModelTests
    {
        [Fact]
        public void LoadsSummariesInPersistedOrderAndSelectsFirst()
        {
            var context = CreateContext();

            Assert.Equal(["첫 요약", "둘째 요약"], context.ViewModel.Summaries.Select(item => item.Title).ToList());
            Assert.Same(context.FirstSummary, context.ViewModel.Summaries[0].Summary);
            Assert.Same(context.SecondSummary, context.ViewModel.Summaries[1].Summary);
            Assert.Same(context.ViewModel.Summaries[0], context.ViewModel.SelectedItem);
            Assert.Equal("첫 요약", context.ViewModel.EditTitle);
            Assert.False(context.ViewModel.IsDirty);
        }

        [Fact]
        public void EmptySummariesHaveNoSelection()
        {
            var context = CreateContext(summaryCount: 0);

            Assert.Empty(context.ViewModel.Summaries);
            Assert.Null(context.ViewModel.SelectedItem);
            Assert.True(context.ViewModel.HasNoSummaries);
            Assert.False(context.ViewModel.HasSelectedItem);
            Assert.False(context.ViewModel.SaveCommand.CanExecute(null));
        }

        [Fact]
        public void EditingBufferDoesNotMutateDomainAndDiscardRestoresValues()
        {
            var context = CreateContext();

            context.ViewModel.EditTitle = "수정 중";
            context.ViewModel.EditCurrentSituation = "수정 상황";

            Assert.True(context.ViewModel.IsDirty);
            Assert.Equal("첫 요약", context.FirstSummary.Title);
            Assert.Equal("첫 요약-상황", context.FirstSummary.CurrentSituation);
            Assert.False(context.ViewModel.IsListInteractionEnabled);
            Assert.True(context.ViewModel.DiscardChangesCommand.CanExecute(null));

            context.ViewModel.DiscardChangesCommand.Execute(null);

            Assert.Equal("첫 요약", context.ViewModel.EditTitle);
            Assert.Equal("첫 요약-상황", context.ViewModel.EditCurrentSituation);
            Assert.False(context.ViewModel.IsDirty);
            Assert.True(context.ViewModel.IsListInteractionEnabled);
            Assert.Equal("", context.ViewModel.ErrorMessage);
        }

        [Fact]
        public void DirtyStatePreventsSelectingAnotherSummary()
        {
            var context = CreateContext();
            var original = context.ViewModel.SelectedItem;

            context.ViewModel.EditTitle = "수정 중";
            context.ViewModel.SelectedItem = context.ViewModel.Summaries[1];

            Assert.Same(original, context.ViewModel.SelectedItem);
            Assert.Equal("수정 중", context.ViewModel.EditTitle);
            Assert.True(context.ViewModel.IsDirty);
        }

        [Fact]
        public async Task SaveUsesExactSelectedReferenceAndEditedValues()
        {
            var duplicateId = Guid.NewGuid();
            var context = CreateContext(useDuplicateIds: true);
            context.FirstSummary.Id = duplicateId;
            context.SecondSummary.Id = duplicateId;
            context.ViewModel.SelectedItem = context.ViewModel.Summaries[1];
            context.ViewModel.EditTitle = " 둘째 수정 ";
            context.ViewModel.EditCurrentSituation = "";
            context.ViewModel.EditKeyEvents = "수정 사건";

            await context.ViewModel.SaveCommand.ExecuteAsync(null);

            Assert.Equal("첫 요약", context.FirstSummary.Title);
            Assert.Equal("둘째 수정", context.SecondSummary.Title);
            Assert.Equal("없음", context.SecondSummary.CurrentSituation);
            Assert.Equal("수정 사건", context.SecondSummary.KeyEvents);
            Assert.Equal(context.SecondSummary.Title, context.ViewModel.EditTitle);
            Assert.Equal(context.SecondSummary.CurrentSituation, context.ViewModel.EditCurrentSituation);
            Assert.False(context.ViewModel.IsDirty);
            Assert.Equal("둘째 수정", context.ViewModel.SelectedItem!.Title);
            var repository = Assert.IsType<FakeWorldRepository>(context.Repository);
            Assert.Equal(1, repository.SaveCount);
        }

        [Fact]
        public async Task SavePreservesRangeAndRefreshesMetadata()
        {
            var context = CreateContext();
            var start = context.FirstSummary.StartMessageId;
            var end = context.FirstSummary.EndMessageId;
            var originalRevision = context.FirstSummary.Revision;

            context.ViewModel.EditTitle = "수정";
            await context.ViewModel.SaveCommand.ExecuteAsync(null);

            Assert.Equal(start, context.FirstSummary.StartMessageId);
            Assert.Equal(end, context.FirstSummary.EndMessageId);
            Assert.Equal(originalRevision + 1, context.FirstSummary.Revision);
            Assert.Contains(context.FirstSummary.UpdatedAt.ToString("yyyy-MM-dd"), context.ViewModel.SelectedItem!.UpdatedAtText);
            Assert.False(context.ViewModel.IsDirty);
        }

        [Fact]
        public async Task SaveFailurePreservesEditBufferAndSelection()
        {
            var repository = new FakeWorldRepository { SaveException = new InvalidOperationException("저장 실패") };
            var context = CreateContext(repository: repository);
            var selected = context.ViewModel.SelectedItem;
            context.ViewModel.EditTitle = "실패해도 남을 제목";

            await context.ViewModel.SaveCommand.ExecuteAsync(null);

            Assert.Same(selected, context.ViewModel.SelectedItem);
            Assert.Equal("실패해도 남을 제목", context.ViewModel.EditTitle);
            Assert.Equal("첫 요약", context.FirstSummary.Title);
            Assert.True(context.ViewModel.IsDirty);
            Assert.True(context.ViewModel.HasErrorMessage);
        }

        [Fact]
        public void TitleValidationControlsSaveCommand()
        {
            var context = CreateContext();

            context.ViewModel.EditTitle = "";
            Assert.False(context.ViewModel.SaveCommand.CanExecute(null));

            context.ViewModel.EditTitle = new string('가', ConversationSummaryService.MaxTitleLength + 1);
            Assert.False(context.ViewModel.SaveCommand.CanExecute(null));

            context.ViewModel.EditTitle = "유효한 제목";
            Assert.True(context.ViewModel.SaveCommand.CanExecute(null));
        }

        [Fact]
        public async Task SavingDisablesEditorListDiscardAndDuplicateSave()
        {
            var repository = new BlockingWorldRepository();
            var context = CreateContext(repository: repository);
            context.ViewModel.EditTitle = "저장 중";

            var saveTask = context.ViewModel.SaveCommand.ExecuteAsync(null);
            await repository.SaveStarted.Task;

            Assert.True(context.ViewModel.IsSaving);
            Assert.True(context.ViewModel.IsBusy);
            Assert.False(context.ViewModel.IsEditorEnabled);
            Assert.False(context.ViewModel.IsListInteractionEnabled);
            Assert.False(context.ViewModel.SaveCommand.CanExecute(null));
            Assert.False(context.ViewModel.DiscardChangesCommand.CanExecute(null));

            repository.Complete();
            await saveTask;

            Assert.False(context.ViewModel.IsSaving);
            Assert.False(context.ViewModel.IsDirty);
        }

        private static ManagementTestContext CreateContext(
            int summaryCount = 2,
            bool useDuplicateIds = false,
            IWorldRepository? repository = null)
        {
            var messages = Enumerable.Range(1, 4)
                .Select(i => new ChatMessage(i % 2 == 0 ? ChatRole.Assistant : ChatRole.User, $"message-{i}"))
                .ToList();
            var first = CreateSummary(messages[0].Id, messages[0].Id, "첫 요약", revision: 1);
            var second = CreateSummary(messages[1].Id, messages[1].Id, "둘째 요약", revision: 2);
            if (useDuplicateIds)
                second.Id = first.Id;

            var session = new ChatSession
            {
                Id = "session",
                WorldId = "world",
                CharacterId = "character",
                UserPersonaId = "user",
                Messages = messages
            };
            if (summaryCount >= 1)
                session.Summaries.Add(first);
            if (summaryCount >= 2)
                session.Summaries.Add(second);

            var character = new Character { Id = "character", Name = "캐릭터" };
            var world = new World
            {
                Id = "world",
                Name = "세계",
                Characters = [character],
                UserPersonas = [new UserPersona { Id = "user", Name = "나" }],
                ChatSessions = [session],
                ActiveCharacterId = character.Id
            };
            var store = new WorldStore { Worlds = [world], ActiveWorldId = world.Id };
            repository ??= new FakeWorldRepository();
            var request = new SummaryManagementRequest(store, session, character, "captured-model");
            var viewModel = new SummaryManagementViewModel(
                new ConversationSummaryPersistenceService(new ConversationSummaryService(), repository),
                request);
            return new ManagementTestContext(
                store,
                session,
                first,
                second,
                repository,
                viewModel);
        }

        private static ConversationSummary CreateSummary(Guid start, Guid end, string title, int revision) =>
            new()
            {
                StartMessageId = start,
                EndMessageId = end,
                Title = title,
                CurrentSituation = $"{title}-상황",
                KeyEvents = $"{title}-사건",
                RelationshipChanges = $"{title}-관계",
                PromisesAndImportantStatements = $"{title}-약속",
                UnresolvedMatters = $"{title}-미해결",
                PersistentState = $"{title}-상태",
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(revision),
                UpdatedAt = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero).AddDays(revision),
                Revision = revision
            };

        private sealed record ManagementTestContext(
            WorldStore Store,
            ChatSession Session,
            ConversationSummary FirstSummary,
            ConversationSummary SecondSummary,
            IWorldRepository Repository,
            SummaryManagementViewModel ViewModel);

        private sealed class BlockingWorldRepository : IWorldRepository
        {
            private readonly TaskCompletionSource<bool> _saveResult = new();
            public TaskCompletionSource<bool> SaveStarted { get; } = new();

            public Task<WorldStore> LoadAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(new WorldStore());

            public async Task SaveAsync(WorldStore store, CancellationToken cancellationToken = default)
            {
                SaveStarted.TrySetResult(true);
                await _saveResult.Task;
            }

            public void Complete()
            {
                _saveResult.SetResult(true);
            }
        }
    }
}
