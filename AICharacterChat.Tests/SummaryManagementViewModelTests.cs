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

        [Fact]
        public async Task RegenerateUsesExactTargetReferenceCapturedModelAndDoesNotSave()
        {
            var duplicateId = Guid.NewGuid();
            var client = new QueueChatModelClient(ValidSummaryJson("AI 둘째", keyEvents: "AI 사건"));
            var context = CreateContext(chatClient: client, useDuplicateIds: true);
            context.FirstSummary.Id = duplicateId;
            context.SecondSummary.Id = duplicateId;
            context.ViewModel.SelectedItem = context.ViewModel.Summaries[1];

            await context.ViewModel.RegenerateCommand.ExecuteAsync(null);

            var request = Assert.Single(client.Requests);
            Assert.Equal("captured-model", request.Model);
            Assert.DoesNotContain("message-1", request.Messages[0].Content);
            Assert.Contains("message-2", request.Messages[0].Content);
            Assert.Equal("첫 요약", context.FirstSummary.Title);
            Assert.Equal("둘째 요약", context.SecondSummary.Title);
            Assert.Equal("AI 둘째", context.ViewModel.EditTitle);
            Assert.Equal("AI 사건", context.ViewModel.EditKeyEvents);
            Assert.True(context.ViewModel.IsDirty);
            var repository = Assert.IsType<FakeWorldRepository>(context.Repository);
            Assert.Equal(0, repository.SaveCount);
        }

        [Fact]
        public async Task RegenerateFailurePreservesEditBufferAndDomain()
        {
            var client = new QueueChatModelClient("not json");
            var context = CreateContext(chatClient: client);
            context.ViewModel.EditTitle = "사용자 제목";
            context.ViewModel.EditCurrentSituation = "사용자 상황";
            context.ViewModel.RegenerateConfirmationRequested += (_, e) => e.Confirmed = true;

            await context.ViewModel.RegenerateCommand.ExecuteAsync(null);

            Assert.Equal("사용자 제목", context.ViewModel.EditTitle);
            Assert.Equal("사용자 상황", context.ViewModel.EditCurrentSituation);
            Assert.Equal("첫 요약", context.FirstSummary.Title);
            Assert.True(context.ViewModel.HasErrorMessage);
        }

        [Fact]
        public async Task DirtyRegenerateDeclineDoesNotCallAiOrChangeBuffer()
        {
            var client = new QueueChatModelClient(ValidSummaryJson("AI 제목"));
            var context = CreateContext(chatClient: client);
            context.ViewModel.EditTitle = "사용자 제목";
            context.ViewModel.RegenerateConfirmationRequested += (_, e) => e.Confirmed = false;

            await context.ViewModel.RegenerateCommand.ExecuteAsync(null);

            Assert.Empty(client.Requests);
            Assert.Equal("사용자 제목", context.ViewModel.EditTitle);
            Assert.Equal("첫 요약", context.FirstSummary.Title);
        }

        [Fact]
        public async Task DirtyRegenerateAcceptRunsAi()
        {
            var client = new QueueChatModelClient(ValidSummaryJson("AI 제목"));
            var context = CreateContext(chatClient: client);
            context.ViewModel.EditTitle = "사용자 제목";
            context.ViewModel.RegenerateConfirmationRequested += (_, e) => e.Confirmed = true;

            await context.ViewModel.RegenerateCommand.ExecuteAsync(null);

            Assert.Single(client.Requests);
            Assert.Equal("AI 제목", context.ViewModel.EditTitle);
            Assert.Equal("첫 요약", context.FirstSummary.Title);
        }

        [Fact]
        public async Task LateRegenerateSuccessAfterWindowClosingDoesNotUpdateBufferOrDomain()
        {
            var client = new ManualChatModelClient();
            var context = CreateContext(chatClient: client);

            var regenerateTask = context.ViewModel.RegenerateCommand.ExecuteAsync(null);
            await client.WaitForCallAsync(1);
            context.ViewModel.OnWindowClosing();
            client.CompleteNext(ValidSummaryJson("늦은 성공"));
            await regenerateTask;

            Assert.Equal("첫 요약", context.ViewModel.EditTitle);
            Assert.Equal("첫 요약", context.FirstSummary.Title);
            Assert.Empty(context.ViewModel.ErrorMessage);
        }

        [Fact]
        public async Task LateRegenerateFailureAfterWindowClosingDoesNotUpdateError()
        {
            var client = new ManualChatModelClient();
            var context = CreateContext(chatClient: client);

            var regenerateTask = context.ViewModel.RegenerateCommand.ExecuteAsync(null);
            await client.WaitForCallAsync(1);
            context.ViewModel.OnWindowClosing();
            client.CompleteNext("not json");
            await regenerateTask;

            Assert.Equal("첫 요약", context.ViewModel.EditTitle);
            Assert.Equal("첫 요약", context.FirstSummary.Title);
            Assert.Empty(context.ViewModel.ErrorMessage);
        }

        [Fact]
        public void InvalidRangeDisablesOnlyRegenerate()
        {
            var context = CreateContext();
            context.FirstSummary.StartMessageId = Guid.NewGuid();

            Assert.False(context.ViewModel.RegenerateCommand.CanExecute(null));
            Assert.True(context.ViewModel.HasRegenerateUnavailableMessage);

            context.ViewModel.EditTitle = "수동 수정";
            Assert.True(context.ViewModel.SaveCommand.CanExecute(null));
            Assert.True(context.ViewModel.DeleteCommand.CanExecute(null));
        }

        [Fact]
        public async Task DeleteUsesExactTargetReferenceAndPreservesMessages()
        {
            var duplicateId = Guid.NewGuid();
            var context = CreateContext(useDuplicateIds: true);
            context.FirstSummary.Id = duplicateId;
            context.SecondSummary.Id = duplicateId;
            context.ViewModel.SelectedItem = context.ViewModel.Summaries[1];
            var originalMessages = context.Session.Messages.ToList();
            context.ViewModel.DeleteConfirmationRequested += (_, e) => e.Confirmed = true;

            await context.ViewModel.DeleteCommand.ExecuteAsync(null);

            Assert.Contains(context.FirstSummary, context.Session.Summaries);
            Assert.DoesNotContain(context.SecondSummary, context.Session.Summaries);
            Assert.Single(context.ViewModel.Summaries);
            Assert.Same(context.FirstSummary, context.ViewModel.Summaries[0].Summary);
            Assert.Null(context.ViewModel.SelectedItem);
            Assert.Equal(originalMessages, context.Session.Messages);
        }

        [Fact]
        public async Task DeleteSuccessClearsSelectionAndBuffer()
        {
            var context = CreateContext(summaryCount: 1);
            context.ViewModel.EditTitle = "삭제 전 수정";
            context.ViewModel.DeleteConfirmationRequested += (_, e) => e.Confirmed = true;

            await context.ViewModel.DeleteCommand.ExecuteAsync(null);

            Assert.Empty(context.Session.Summaries);
            Assert.Empty(context.ViewModel.Summaries);
            Assert.Null(context.ViewModel.SelectedItem);
            Assert.Equal("", context.ViewModel.EditTitle);
            Assert.False(context.ViewModel.IsDirty);
            Assert.True(context.ViewModel.HasNoSummaries);
            Assert.False(context.ViewModel.HasSummaries);
        }

        [Fact]
        public async Task DeleteWithRemainingSummariesLeavesNoSelectionState()
        {
            var context = CreateContext();
            context.ViewModel.DeleteConfirmationRequested += (_, e) => e.Confirmed = true;

            await context.ViewModel.DeleteCommand.ExecuteAsync(null);

            Assert.Single(context.ViewModel.Summaries);
            Assert.Null(context.ViewModel.SelectedItem);
            Assert.True(context.ViewModel.HasNoSelectionWithSummaries);
            Assert.False(context.ViewModel.HasSelectedItem);
        }

        [Fact]
        public async Task DeleteFailurePreservesWrapperSelectionBufferAndDomain()
        {
            var repository = new FakeWorldRepository { SaveException = new InvalidOperationException("저장 실패") };
            var context = CreateContext(repository: repository);
            var selected = context.ViewModel.SelectedItem;
            context.ViewModel.EditTitle = "삭제 실패 전 수정";
            context.ViewModel.DeleteConfirmationRequested += (_, e) => e.Confirmed = true;

            await context.ViewModel.DeleteCommand.ExecuteAsync(null);

            Assert.Contains(context.FirstSummary, context.Session.Summaries);
            Assert.Contains(context.ViewModel.Summaries, item => ReferenceEquals(item.Summary, context.FirstSummary));
            Assert.Same(selected, context.ViewModel.SelectedItem);
            Assert.Equal("삭제 실패 전 수정", context.ViewModel.EditTitle);
            Assert.True(context.ViewModel.IsDirty);
            Assert.True(context.ViewModel.HasErrorMessage);
        }

        [Fact]
        public async Task DirtyDeleteConfirmationReportsUnsavedChanges()
        {
            var context = CreateContext();
            bool? hasUnsavedChanges = null;
            context.ViewModel.EditTitle = "삭제 전 수정";
            context.ViewModel.DeleteConfirmationRequested += (_, e) =>
            {
                hasUnsavedChanges = e.HasUnsavedChanges;
                e.Confirmed = false;
            };

            await context.ViewModel.DeleteCommand.ExecuteAsync(null);

            Assert.True(hasUnsavedChanges);
            Assert.Contains(context.FirstSummary, context.Session.Summaries);
            Assert.Equal("삭제 전 수정", context.ViewModel.EditTitle);
        }

        [Fact]
        public async Task RegeneratingDisablesMutatingActionsAndEditing()
        {
            var client = new ManualChatModelClient();
            var context = CreateContext(chatClient: client);

            var regenerateTask = context.ViewModel.RegenerateCommand.ExecuteAsync(null);
            await client.WaitForCallAsync(1);

            Assert.True(context.ViewModel.IsRegenerating);
            Assert.True(context.ViewModel.IsBusy);
            Assert.False(context.ViewModel.IsEditorEnabled);
            Assert.False(context.ViewModel.IsListInteractionEnabled);
            Assert.False(context.ViewModel.SaveCommand.CanExecute(null));
            Assert.False(context.ViewModel.DeleteCommand.CanExecute(null));
            Assert.False(context.ViewModel.RegenerateCommand.CanExecute(null));
            Assert.False(context.ViewModel.DiscardChangesCommand.CanExecute(null));
            var original = context.ViewModel.SelectedItem;
            context.ViewModel.SelectedItem = context.ViewModel.Summaries[1];
            Assert.Same(original, context.ViewModel.SelectedItem);

            client.CompleteNext(ValidSummaryJson("AI 제목"));
            await regenerateTask;
        }

        [Fact]
        public async Task DeletingDisablesMutatingActionsAndEditing()
        {
            var repository = new BlockingWorldRepository();
            var context = CreateContext(repository: repository);
            context.ViewModel.DeleteConfirmationRequested += (_, e) => e.Confirmed = true;

            var deleteTask = context.ViewModel.DeleteCommand.ExecuteAsync(null);
            await repository.SaveStarted.Task;

            Assert.True(context.ViewModel.IsDeleting);
            Assert.True(context.ViewModel.IsBusy);
            Assert.False(context.ViewModel.IsEditorEnabled);
            Assert.False(context.ViewModel.IsListInteractionEnabled);
            Assert.False(context.ViewModel.SaveCommand.CanExecute(null));
            Assert.False(context.ViewModel.DeleteCommand.CanExecute(null));
            Assert.False(context.ViewModel.RegenerateCommand.CanExecute(null));
            Assert.False(context.ViewModel.DiscardChangesCommand.CanExecute(null));
            var original = context.ViewModel.SelectedItem;
            context.ViewModel.SelectedItem = context.ViewModel.Summaries[1];
            Assert.Same(original, context.ViewModel.SelectedItem);

            repository.Complete();
            await deleteTask;
        }

        private static ManagementTestContext CreateContext(
            int summaryCount = 2,
            bool useDuplicateIds = false,
            IWorldRepository? repository = null,
            IChatModelClient? chatClient = null)
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
            chatClient ??= new QueueChatModelClient(ValidSummaryJson("AI 요약"));
            var summaryService = new ConversationSummaryService();
            var request = new SummaryManagementRequest(store, session, character, "captured-model");
            var viewModel = new SummaryManagementViewModel(
                new ConversationSummaryPersistenceService(summaryService, repository),
                new ConversationSummarizer(chatClient, summaryService),
                request);
            return new ManagementTestContext(
                store,
                session,
                first,
                second,
                repository,
                viewModel);
        }

        private static string ValidSummaryJson(
            string title,
            string currentSituation = "AI 상황",
            string keyEvents = "AI 사건") =>
            $$"""
              {
                "title": "{{title}}",
                "currentSituation": "{{currentSituation}}",
                "keyEvents": "{{keyEvents}}",
                "relationshipChanges": "AI 관계",
                "promisesAndImportantStatements": "AI 약속",
                "unresolvedMatters": "AI 미해결",
                "persistentState": "AI 상태"
              }
              """;

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

        private sealed class QueueChatModelClient : IChatModelClient
        {
            private readonly Queue<string> _replies;
            public List<ChatCompletionRequest> Requests { get; } = [];

            public QueueChatModelClient(params string[] replies)
            {
                _replies = new Queue<string>(replies);
            }

            public Task<string> SendAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
            {
                Requests.Add(request);
                return Task.FromResult(_replies.Count > 0 ? _replies.Dequeue() : "");
            }
        }

        private sealed class ManualChatModelClient : IChatModelClient
        {
            private readonly Queue<TaskCompletionSource<string>> _pendingReplies = new();
            private readonly SemaphoreSlim _callArrived = new(0);
            public List<ChatCompletionRequest> Requests { get; } = [];

            public Task<string> SendAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
            {
                Requests.Add(request);
                var reply = new TaskCompletionSource<string>();
                _pendingReplies.Enqueue(reply);
                _callArrived.Release();
                return reply.Task;
            }

            public async Task WaitForCallAsync(int count)
            {
                while (Requests.Count < count)
                    await _callArrived.WaitAsync();
            }

            public void CompleteNext(string reply)
            {
                _pendingReplies.Dequeue().SetResult(reply);
            }
        }
    }
}
