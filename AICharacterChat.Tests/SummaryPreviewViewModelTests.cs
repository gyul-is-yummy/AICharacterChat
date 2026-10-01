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
    public class SummaryPreviewViewModelTests
    {
        [Fact]
        public async Task InitializeSuccessPopulatesFields()
        {
            var context = CreateContext(new QueueChatModelClient(ValidSummaryJson("첫 요약")));

            await context.ViewModel.InitializeAsync();

            Assert.True(context.ViewModel.HasDraft);
            Assert.Equal("첫 요약", context.ViewModel.Title);
            Assert.Equal("현재 상황", context.ViewModel.CurrentSituation);
            Assert.Equal("주요 사건", context.ViewModel.KeyEvents);
            Assert.Equal("관계 변화", context.ViewModel.RelationshipChanges);
            Assert.Equal("약속", context.ViewModel.PromisesAndImportantStatements);
            Assert.Equal("미해결", context.ViewModel.UnresolvedMatters);
            Assert.Equal("지속 상태", context.ViewModel.PersistentState);
            Assert.Empty(context.Session.Summaries);
        }

        [Fact]
        public async Task InitializeFailureKeepsWindowUsable()
        {
            var context = CreateContext(new QueueChatModelClient("not json"));

            await context.ViewModel.InitializeAsync();

            Assert.False(context.ViewModel.HasDraft);
            Assert.True(context.ViewModel.HasErrorMessage);
            Assert.True(context.ViewModel.RegenerateCommand.CanExecute(null));
            Assert.False(context.ViewModel.SaveCommand.CanExecute(null));
            Assert.Empty(context.Session.Summaries);
        }

        [Fact]
        public async Task RegenerateSuccessReplacesFieldsAndUsesOriginalContext()
        {
            var client = new QueueChatModelClient(
                ValidSummaryJson("첫 요약"),
                ValidSummaryJson("새 요약", keyEvents: "새 사건"));
            var context = CreateContext(client);
            await context.ViewModel.InitializeAsync();
            context.ViewModel.KeyEvents = "사용자 편집";

            await context.ViewModel.RegenerateCommand.ExecuteAsync(null);

            Assert.Equal("새 요약", context.ViewModel.Title);
            Assert.Equal("새 사건", context.ViewModel.KeyEvents);
            Assert.Equal(2, client.Requests.Count);
            Assert.All(client.Requests, request => Assert.Equal("captured-model", request.Model));
            Assert.All(client.Requests, request => Assert.Contains("message-1", request.Messages[0].Content));
            Assert.All(client.Requests, request => Assert.Contains("message-3", request.Messages[0].Content));
        }

        [Fact]
        public async Task RegenerateFailurePreservesExistingFields()
        {
            var client = new QueueChatModelClient(ValidSummaryJson("첫 요약"), "not json");
            var context = CreateContext(client);
            await context.ViewModel.InitializeAsync();
            context.ViewModel.Title = "사용자 제목";
            context.ViewModel.CurrentSituation = "사용자 상황";

            await context.ViewModel.RegenerateCommand.ExecuteAsync(null);

            Assert.Equal("사용자 제목", context.ViewModel.Title);
            Assert.Equal("사용자 상황", context.ViewModel.CurrentSituation);
            Assert.True(context.ViewModel.HasErrorMessage);
            Assert.Empty(context.Session.Summaries);
        }

        [Fact]
        public async Task SaveUsesEditedValuesAndOriginalRange()
        {
            var context = CreateContext(new QueueChatModelClient(ValidSummaryJson("첫 요약")));
            await context.ViewModel.InitializeAsync();
            context.ViewModel.Title = "수정 제목";
            context.ViewModel.CurrentSituation = "수정 상황";
            context.ViewModel.KeyEvents = "수정 사건";
            context.ViewModel.RelationshipChanges = "수정 관계";
            context.ViewModel.PromisesAndImportantStatements = "수정 약속";
            context.ViewModel.UnresolvedMatters = "수정 미해결";
            context.ViewModel.PersistentState = "수정 상태";

            await context.ViewModel.SaveCommand.ExecuteAsync(null);

            var summary = Assert.Single(context.Session.Summaries);
            Assert.Equal(context.Session.Messages[0].Id, summary.StartMessageId);
            Assert.Equal(context.Session.Messages[2].Id, summary.EndMessageId);
            Assert.Equal("수정 제목", summary.Title);
            Assert.Equal("수정 상황", summary.CurrentSituation);
            Assert.Equal("수정 사건", summary.KeyEvents);
            Assert.Equal("수정 관계", summary.RelationshipChanges);
            Assert.Equal("수정 약속", summary.PromisesAndImportantStatements);
            Assert.Equal("수정 미해결", summary.UnresolvedMatters);
            Assert.Equal("수정 상태", summary.PersistentState);
        }

        [Fact]
        public async Task SaveSuccessRequestsSavedClose()
        {
            var context = CreateContext(new QueueChatModelClient(ValidSummaryJson("첫 요약")));
            await context.ViewModel.InitializeAsync();
            SummaryPreviewResult? closeResult = null;
            context.ViewModel.CloseRequested += (_, e) => closeResult = e.Result;

            await context.ViewModel.SaveCommand.ExecuteAsync(null);

            Assert.Equal(SummaryPreviewResult.Saved, closeResult);
        }

        [Fact]
        public async Task SaveFailurePreservesEditedFieldsAndKeepsWindowOpen()
        {
            var repository = new FakeWorldRepository { SaveException = new InvalidOperationException("저장 실패") };
            var context = CreateContext(new QueueChatModelClient(ValidSummaryJson("첫 요약")), repository);
            await context.ViewModel.InitializeAsync();
            context.ViewModel.Title = "보존 제목";
            SummaryPreviewResult? closeResult = null;
            context.ViewModel.CloseRequested += (_, e) => closeResult = e.Result;

            await context.ViewModel.SaveCommand.ExecuteAsync(null);

            Assert.Null(closeResult);
            Assert.Equal("보존 제목", context.ViewModel.Title);
            Assert.True(context.ViewModel.HasErrorMessage);
            Assert.Empty(context.Session.Summaries);
        }

        [Fact]
        public async Task CancelDoesNotPersistAndRequestsCanceledClose()
        {
            var context = CreateContext(new QueueChatModelClient(ValidSummaryJson("첫 요약")));
            await context.ViewModel.InitializeAsync();
            SummaryPreviewResult? closeResult = null;
            context.ViewModel.CloseRequested += (_, e) => closeResult = e.Result;

            context.ViewModel.CancelCommand.Execute(null);

            Assert.Equal(SummaryPreviewResult.Canceled, closeResult);
            Assert.Empty(context.Session.Summaries);
        }

        [Fact]
        public async Task SaveDisabledBeforeDraftAndForInvalidTitle()
        {
            var context = CreateContext(new QueueChatModelClient(ValidSummaryJson("첫 요약")));

            Assert.False(context.ViewModel.SaveCommand.CanExecute(null));

            await context.ViewModel.InitializeAsync();
            Assert.True(context.ViewModel.SaveCommand.CanExecute(null));

            context.ViewModel.Title = "   ";
            Assert.False(context.ViewModel.SaveCommand.CanExecute(null));

            context.ViewModel.Title = new string('가', ConversationSummaryService.MaxTitleLength + 1);
            Assert.False(context.ViewModel.SaveCommand.CanExecute(null));

            context.ViewModel.Title = "유효 제목";
            Assert.True(context.ViewModel.SaveCommand.CanExecute(null));
        }

        [Fact]
        public async Task BusyDisablesRegenerateAndSave()
        {
            var client = new BlockingChatModelClient();
            var context = CreateContext(client);

            var initializeTask = context.ViewModel.InitializeAsync();
            await client.CallStarted.Task;

            Assert.True(context.ViewModel.IsBusy);
            Assert.False(context.ViewModel.RegenerateCommand.CanExecute(null));
            Assert.False(context.ViewModel.SaveCommand.CanExecute(null));

            client.Complete(ValidSummaryJson("첫 요약"));
            await initializeTask;

            Assert.False(context.ViewModel.IsBusy);
            Assert.True(context.ViewModel.RegenerateCommand.CanExecute(null));
            Assert.True(context.ViewModel.SaveCommand.CanExecute(null));
        }

        [Fact]
        public async Task InitializeAndRegenerateDoNotMutateDomain()
        {
            var client = new QueueChatModelClient(
                ValidSummaryJson("첫 요약"),
                ValidSummaryJson("새 요약"));
            var context = CreateContext(client);

            await context.ViewModel.InitializeAsync();
            await context.ViewModel.RegenerateCommand.ExecuteAsync(null);

            Assert.Empty(context.Session.Summaries);
        }

        [Fact]
        public async Task LateInitialSuccessAfterWindowClosingDoesNotUpdateState()
        {
            var client = new ManualChatModelClient();
            var context = CreateContext(client);
            SummaryPreviewResult? closeResult = null;
            context.ViewModel.CloseRequested += (_, e) => closeResult = e.Result;

            var initializeTask = context.ViewModel.InitializeAsync();
            await client.WaitForCallAsync(1);
            context.ViewModel.OnWindowClosing();
            client.CompleteNext(ValidSummaryJson("늦은 성공"));
            await initializeTask;

            Assert.False(context.ViewModel.HasDraft);
            Assert.Equal("", context.ViewModel.Title);
            Assert.Empty(context.ViewModel.ErrorMessage);
            Assert.Null(closeResult);
            Assert.Empty(context.Session.Summaries);
        }

        [Fact]
        public async Task LateInitialFailureAfterWindowClosingDoesNotUpdateError()
        {
            var client = new ManualChatModelClient();
            var context = CreateContext(client);

            var initializeTask = context.ViewModel.InitializeAsync();
            await client.WaitForCallAsync(1);
            context.ViewModel.OnWindowClosing();
            client.CompleteNext("not json");
            await initializeTask;

            Assert.False(context.ViewModel.HasDraft);
            Assert.Empty(context.ViewModel.ErrorMessage);
            Assert.Empty(context.Session.Summaries);
        }

        [Fact]
        public async Task LateRegenerateSuccessAfterWindowClosingDoesNotReplaceEditedFields()
        {
            var client = new ManualChatModelClient();
            var context = CreateContext(client);
            var initializeTask = context.ViewModel.InitializeAsync();
            await client.WaitForCallAsync(1);
            client.CompleteNext(ValidSummaryJson("첫 요약"));
            await initializeTask;
            context.ViewModel.Title = "사용자 제목";
            context.ViewModel.KeyEvents = "사용자 사건";

            var regenerateTask = context.ViewModel.RegenerateCommand.ExecuteAsync(null);
            await client.WaitForCallAsync(2);
            context.ViewModel.OnWindowClosing();
            client.CompleteNext(ValidSummaryJson("늦은 재생성", keyEvents: "늦은 사건"));
            await regenerateTask;

            Assert.Equal("사용자 제목", context.ViewModel.Title);
            Assert.Equal("사용자 사건", context.ViewModel.KeyEvents);
            Assert.Empty(context.Session.Summaries);
        }

        [Fact]
        public async Task CancelCommandIsDisabledDuringSaveAndReenabledAfterFailure()
        {
            var repository = new BlockingWorldRepository();
            var context = CreateContext(new QueueChatModelClient(ValidSummaryJson("첫 요약")), repository);
            await context.ViewModel.InitializeAsync();
            var canExecuteChangedCount = 0;
            context.ViewModel.CancelCommand.CanExecuteChanged += (_, _) => canExecuteChangedCount++;

            var saveTask = context.ViewModel.SaveCommand.ExecuteAsync(null);
            await repository.SaveStarted.Task;

            Assert.True(context.ViewModel.IsSaving);
            Assert.False(context.ViewModel.CancelCommand.CanExecute(null));

            repository.Fail(new InvalidOperationException("저장 실패"));
            await saveTask;

            Assert.False(context.ViewModel.IsSaving);
            Assert.True(context.ViewModel.CancelCommand.CanExecute(null));
            Assert.True(canExecuteChangedCount >= 2);
        }

        private static PreviewTestContext CreateContext(
            IChatModelClient chatClient,
            IWorldRepository? repository = null)
        {
            var world = new World
            {
                Id = "world",
                Name = "세계",
                Characters = [new Character { Id = "character", Name = "캐릭터" }]
            };
            var session = new ChatSession
            {
                Id = "session",
                WorldId = world.Id,
                CharacterId = world.Characters[0].Id,
                Messages =
                [
                    new ChatMessage(ChatRole.User, "message-1"),
                    new ChatMessage(ChatRole.Assistant, "message-2"),
                    new ChatMessage(ChatRole.User, "message-3")
                ]
            };
            world.ChatSessions.Add(session);
            var store = new WorldStore { ActiveWorldId = world.Id, Worlds = [world] };
            var summaryService = new ConversationSummaryService();
            repository ??= new FakeWorldRepository();
            var request = new SummaryPreviewRequest(
                store,
                session,
                world.Characters[0],
                session.Messages[0].Id,
                session.Messages[2].Id,
                "captured-model");
            var viewModel = new SummaryPreviewViewModel(
                new ConversationSummarizer(chatClient, summaryService),
                new ConversationSummaryPersistenceService(summaryService, repository),
                request);
            return new PreviewTestContext(store, session, viewModel);
        }

        private static string ValidSummaryJson(
            string title,
            string currentSituation = "현재 상황",
            string keyEvents = "주요 사건") =>
            $$"""
              {
                "title": "{{title}}",
                "currentSituation": "{{currentSituation}}",
                "keyEvents": "{{keyEvents}}",
                "relationshipChanges": "관계 변화",
                "promisesAndImportantStatements": "약속",
                "unresolvedMatters": "미해결",
                "persistentState": "지속 상태"
              }
              """;

        private sealed record PreviewTestContext(
            WorldStore Store,
            ChatSession Session,
            SummaryPreviewViewModel ViewModel);

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

        private sealed class BlockingChatModelClient : IChatModelClient
        {
            private readonly TaskCompletionSource<string> _reply = new();
            public TaskCompletionSource<bool> CallStarted { get; } = new();

            public async Task<string> SendAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
            {
                CallStarted.TrySetResult(true);
                return await _reply.Task;
            }

            public void Complete(string reply)
            {
                _reply.TrySetResult(reply);
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

        private sealed class BlockingWorldRepository : IWorldRepository
        {
            private readonly TaskCompletionSource<bool> _saveResult = new();
            private Exception? _exception;
            public TaskCompletionSource<bool> SaveStarted { get; } = new();

            public Task<WorldStore> LoadAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(new WorldStore());

            public async Task SaveAsync(WorldStore store, CancellationToken cancellationToken = default)
            {
                SaveStarted.TrySetResult(true);
                await _saveResult.Task;
                if (_exception != null)
                    throw _exception;
            }

            public void Fail(Exception exception)
            {
                _exception = exception;
                _saveResult.SetResult(true);
            }
        }
    }
}
