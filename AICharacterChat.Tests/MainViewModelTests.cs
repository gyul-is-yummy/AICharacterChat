using System;
using System.Linq;
using System.Threading.Tasks;
using AICharacterChat.Application.Chat;
using AICharacterChat.Application.Context;
using AICharacterChat.Application.Interfaces;
using AICharacterChat.Application.Models;
using AICharacterChat.Domain.Enums;
using AICharacterChat.Domain.Models;
using AICharacterChat.Presentation.Models;
using AICharacterChat.Presentation.ViewModels;
using Xunit;

namespace AICharacterChat.Tests
{
    public class MainViewModelTests
    {
        [Fact]
        public async Task ClearChatCommandClearsTargetCharacterSessionOnly()
        {
            var world = TestData.CreateWorld();
            var other = new Character { Id = "char-2", Name = "다른 캐릭터" };
            world.Characters.Add(other);
            var targetSummary = new ConversationSummary
            {
                StartMessageId = world.ChatSessions[0].Messages.FirstOrDefault()?.Id ?? Guid.NewGuid(),
                EndMessageId = world.ChatSessions[0].Messages.FirstOrDefault()?.Id ?? Guid.NewGuid(),
                Title = "지울 요약"
            };
            var otherSession = new ChatSession
            {
                Id = "session-2",
                WorldId = world.Id,
                CharacterId = other.Id,
                UserPersonaId = world.UserPersonas[0].Id,
                Messages = [new ChatMessage(ChatRole.User, "지우면 안 됨")],
                Summaries =
                [
                    new ConversationSummary
                    {
                        StartMessageId = Guid.NewGuid(),
                        EndMessageId = Guid.NewGuid(),
                        Title = "다른 요약"
                    }
                ]
            };
            world.ChatSessions[0].Messages.Add(new ChatMessage(ChatRole.User, "지울 메시지"));
            world.ChatSessions[0].Summaries.Add(targetSummary);
            world.ChatSessions.Add(otherSession);

            var store = new WorldStore { ActiveWorldId = world.Id, Worlds = [world] };
            var vm = CreateViewModel(store);
            await vm.InitializeAsync();
            await vm.ClearChatForCharacterAsync(world.Characters[0]);

            Assert.Empty(world.ChatSessions[0].Messages);
            Assert.Empty(world.ChatSessions[0].Summaries);
            Assert.Single(otherSession.Messages);
            Assert.Single(otherSession.Summaries);
        }

        [Fact]
        public async Task ClearChatSaveFailureRollsBackMessagesAndSummaries()
        {
            var world = TestData.CreateWorld();
            var message = new ChatMessage(ChatRole.User, "보존");
            world.ChatSessions[0].Messages.Add(message);
            var summary = new ConversationSummary
            {
                StartMessageId = message.Id,
                EndMessageId = message.Id,
                Title = "보존 요약"
            };
            world.ChatSessions[0].Summaries.Add(summary);
            var store = new WorldStore { ActiveWorldId = world.Id, Worlds = [world] };
            var repository = new MemoryWorldRepository(store);
            var vm = CreateViewModel(repository);
            await vm.InitializeAsync();
            repository.SaveException = new InvalidOperationException("저장 실패");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => vm.ClearChatForCharacterAsync(world.Characters[0]));

            Assert.Contains(message, world.ChatSessions[0].Messages);
            Assert.Contains(summary, world.ChatSessions[0].Summaries);
        }

        [Fact]
        public async Task AddCharacterUsesSelectedUserPersonaForDefaultSession()
        {
            var world = TestData.CreateWorld();
            world.UserPersonas.Add(new UserPersona { Id = "user-2", Name = "두 번째" });
            var store = new WorldStore { ActiveWorldId = world.Id, Worlds = [world] };
            var vm = CreateViewModel(store);
            await vm.InitializeAsync();
            var character = new Character { Id = "char-new", Name = "새 캐릭터" };

            await vm.AddCharacterAsync(character, "user-2");

            Assert.Equal("user-2", world.GetSessionForCharacter("char-new")!.UserPersonaId);
        }

        [Fact]
        public async Task RefreshMessagesBuildsMessageItemsProjection()
        {
            var world = TestData.CreateWorld();
            var first = new ChatMessage(ChatRole.User, "첫 메시지");
            var second = new ChatMessage(ChatRole.Assistant, "둘째 메시지");
            world.ChatSessions[0].Messages.Add(first);
            world.ChatSessions[0].Messages.Add(second);
            var vm = CreateViewModel(new WorldStore { ActiveWorldId = world.Id, Worlds = [world] });

            await vm.InitializeAsync();

            Assert.Equal(2, vm.Messages.Count);
            Assert.Equal(2, vm.MessageItems.Count);
            Assert.Same(first, vm.MessageItems[0].Message);
            Assert.Same(second, vm.MessageItems[1].Message);
            Assert.Equal("첫 메시지", vm.MessageItems[0].Content);
            Assert.Equal(ChatRole.Assistant, vm.MessageItems[1].Role);
        }

        [Fact]
        public async Task BeginSummarySelectionEntersMode()
        {
            var vm = await CreateInitializedViewModelWithMessagesAsync(2);

            vm.BeginSummarySelectionCommand.Execute(null);

            Assert.True(vm.IsSummarySelectionMode);
            Assert.Equal("시작 메시지를 선택하세요", vm.SummarySelectionStatusText);
        }

        [Fact]
        public async Task BeginWithoutMessagesDoesNotEnterMode()
        {
            var world = TestData.CreateWorld();
            world.ChatSessions[0].Messages.Clear();
            var vm = CreateViewModel(new WorldStore { ActiveWorldId = world.Id, Worlds = [world] });
            await vm.InitializeAsync();

            vm.BeginSummarySelectionCommand.Execute(null);

            Assert.False(vm.IsSummarySelectionMode);
        }

        [Fact]
        public async Task BeginWhileSendingDoesNotEnterMode()
        {
            var world = TestData.CreateWorld();
            world.ChatSessions[0].Messages.Add(new ChatMessage(ChatRole.User, "기존"));
            var store = new WorldStore { ActiveWorldId = world.Id, Worlds = [world] };
            var client = new BlockingChatModelClient();
            var vm = CreateViewModel(store, client);
            await vm.InitializeAsync();
            vm.InputText = "전송 중";

            var sendTask = vm.SendMessageCommand.ExecuteAsync(null);
            await client.CallStarted.Task;

            Assert.True(vm.IsSending);
            Assert.False(vm.BeginSummarySelectionCommand.CanExecute(null));
            vm.BeginSummarySelectionCommand.Execute(null);
            Assert.False(vm.IsSummarySelectionMode);

            client.Complete("답");
            await sendTask;

            Assert.False(vm.IsSending);
            Assert.True(vm.BeginSummarySelectionCommand.CanExecute(null));
        }

        [Fact]
        public async Task BeginCommandRaisesCanExecuteChangedWhenSendingChanges()
        {
            var world = TestData.CreateWorld();
            world.ChatSessions[0].Messages.Add(new ChatMessage(ChatRole.User, "기존"));
            var store = new WorldStore { ActiveWorldId = world.Id, Worlds = [world] };
            var client = new BlockingChatModelClient();
            var vm = CreateViewModel(store, client);
            await vm.InitializeAsync();
            vm.InputText = "전송 중";
            var canExecuteChangedCount = 0;
            vm.BeginSummarySelectionCommand.CanExecuteChanged += (_, _) => canExecuteChangedCount++;

            var sendTask = vm.SendMessageCommand.ExecuteAsync(null);
            await client.CallStarted.Task;

            Assert.False(vm.BeginSummarySelectionCommand.CanExecute(null));
            Assert.True(canExecuteChangedCount >= 1);

            client.Complete("답");
            await sendTask;

            Assert.True(vm.BeginSummarySelectionCommand.CanExecute(null));
            Assert.True(canExecuteChangedCount >= 2);
        }

        [Fact]
        public async Task NextDisabledWithoutCompletedRange()
        {
            var vm = await CreateInitializedViewModelWithMessagesAsync(3);

            vm.BeginSummarySelectionCommand.Execute(null);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[0]);

            Assert.False(vm.OpenSummaryPreviewCommand.CanExecute(null));
        }

        [Fact]
        public async Task NextDisabledForInvalidOverlap()
        {
            var world = TestData.CreateWorld();
            AddMessages(world.ChatSessions[0], 4);
            world.ChatSessions[0].Summaries.Add(new ConversationSummary
            {
                StartMessageId = world.ChatSessions[0].Messages[1].Id,
                EndMessageId = world.ChatSessions[0].Messages[2].Id,
                Title = "기존 요약"
            });
            var vm = CreateViewModel(new WorldStore { ActiveWorldId = world.Id, Worlds = [world] });
            await vm.InitializeAsync();

            vm.BeginSummarySelectionCommand.Execute(null);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[0]);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[3]);

            Assert.False(vm.OpenSummaryPreviewCommand.CanExecute(null));
        }

        [Fact]
        public async Task NextRaisesPreviewRequestForValidRange()
        {
            var world = TestData.CreateWorld();
            AddMessages(world.ChatSessions[0], 3);
            var store = new WorldStore { ActiveWorldId = world.Id, Worlds = [world] };
            var vm = CreateViewModel(store);
            await vm.InitializeAsync();
            SummaryPreviewRequest? request = null;
            vm.SummaryPreviewRequested += (_, e) =>
            {
                request = e.Request;
                e.Result = SummaryPreviewResult.Canceled;
            };

            vm.BeginSummarySelectionCommand.Execute(null);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[0]);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[2]);
            vm.OpenSummaryPreviewCommand.Execute(null);

            Assert.NotNull(request);
            Assert.Same(store, request!.Store);
            Assert.Same(world.ChatSessions[0], request.Session);
            Assert.Same(world.Characters[0], request.Character);
            Assert.Equal(world.ChatSessions[0].Messages[0].Id, request.StartMessageId);
            Assert.Equal(world.ChatSessions[0].Messages[2].Id, request.EndMessageId);
            Assert.Equal("model", request.ModelId);
        }

        [Fact]
        public async Task NextRevalidatesRangeBeforeRequest()
        {
            var world = TestData.CreateWorld();
            AddMessages(world.ChatSessions[0], 4);
            var session = world.ChatSessions[0];
            var vm = CreateViewModel(new WorldStore { ActiveWorldId = world.Id, Worlds = [world] });
            await vm.InitializeAsync();
            var requestRaised = false;
            vm.SummaryPreviewRequested += (_, _) => requestRaised = true;

            vm.BeginSummarySelectionCommand.Execute(null);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[0]);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[3]);
            session.Summaries.Add(new ConversationSummary
            {
                StartMessageId = session.Messages[1].Id,
                EndMessageId = session.Messages[2].Id,
                Title = "늦게 추가된 요약"
            });
            vm.OpenSummaryPreviewCommand.Execute(null);

            Assert.False(requestRaised);
            Assert.Contains("겹칩니다", vm.SummarySelectionError);
        }

        [Fact]
        public async Task PreviewSavedClearsSelection()
        {
            var vm = await CreateInitializedViewModelWithMessagesAsync(3);
            vm.SummaryPreviewRequested += (_, e) => e.Result = SummaryPreviewResult.Saved;

            vm.BeginSummarySelectionCommand.Execute(null);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[0]);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[2]);
            vm.OpenSummaryPreviewCommand.Execute(null);

            Assert.False(vm.IsSummarySelectionMode);
            Assert.Null(vm.SummarySelectionStartMessageId);
            Assert.Null(vm.SummarySelectionEndMessageId);
            Assert.All(vm.MessageItems, item => Assert.False(item.IsInSummarySelectionRange));
        }

        [Fact]
        public async Task PreviewCanceledRetainsSelection()
        {
            var vm = await CreateInitializedViewModelWithMessagesAsync(3);
            vm.SummaryPreviewRequested += (_, e) => e.Result = SummaryPreviewResult.Canceled;

            vm.BeginSummarySelectionCommand.Execute(null);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[0]);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[2]);
            vm.OpenSummaryPreviewCommand.Execute(null);

            Assert.True(vm.IsSummarySelectionMode);
            Assert.NotNull(vm.SummarySelectionStartMessageId);
            Assert.NotNull(vm.SummarySelectionEndMessageId);
            Assert.Equal(3, vm.SummarySelectionMessageCount);
        }

        [Fact]
        public async Task SummaryManagementRequestCapturesCurrentContextEvenWithoutSummaries()
        {
            var world = TestData.CreateWorld();
            world.ChatSessions[0].Summaries.Clear();
            var store = new WorldStore { ActiveWorldId = world.Id, Worlds = [world] };
            var vm = CreateViewModel(store);
            await vm.InitializeAsync();
            SummaryManagementRequest? request = null;
            vm.SummaryManagementRequested += (_, e) => request = e.Request;

            Assert.True(vm.OpenSummaryManagementCommand.CanExecute(null));
            vm.OpenSummaryManagementCommand.Execute(null);

            Assert.NotNull(request);
            Assert.Same(store, request!.Store);
            Assert.Same(world.ChatSessions[0], request.Session);
            Assert.Same(world.Characters[0], request.Character);
            Assert.Equal("model", request.ModelId);
            Assert.Empty(world.ChatSessions[0].Summaries);
        }

        [Fact]
        public async Task SummaryManagementIsDisabledWhileSending()
        {
            var world = TestData.CreateWorld();
            world.ChatSessions[0].Messages.Add(new ChatMessage(ChatRole.User, "기존"));
            var store = new WorldStore { ActiveWorldId = world.Id, Worlds = [world] };
            var client = new BlockingChatModelClient();
            var vm = CreateViewModel(store, client);
            await vm.InitializeAsync();
            vm.InputText = "전송 중";

            var sendTask = vm.SendMessageCommand.ExecuteAsync(null);
            await client.CallStarted.Task;

            Assert.True(vm.IsSending);
            Assert.False(vm.OpenSummaryManagementCommand.CanExecute(null));

            client.Complete("답");
            await sendTask;

            Assert.True(vm.OpenSummaryManagementCommand.CanExecute(null));
        }

        [Fact]
        public async Task FirstClickSetsAnchor()
        {
            var vm = await CreateInitializedViewModelWithMessagesAsync(3);

            vm.BeginSummarySelectionCommand.Execute(null);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[1]);

            Assert.Equal(vm.MessageItems[1].Id, vm.SummarySelectionStartMessageId);
            Assert.Null(vm.SummarySelectionEndMessageId);
            Assert.True(vm.MessageItems[1].IsInSummarySelectionRange);
            Assert.True(vm.MessageItems[1].IsSummarySelectionStart);
            Assert.Equal("끝 메시지를 선택하세요", vm.SummarySelectionStatusText);
        }

        [Fact]
        public async Task SecondClickCreatesRange()
        {
            var vm = await CreateInitializedViewModelWithMessagesAsync(5);

            vm.BeginSummarySelectionCommand.Execute(null);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[1]);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[3]);

            Assert.Equal(vm.MessageItems[1].Id, vm.SummarySelectionStartMessageId);
            Assert.Equal(vm.MessageItems[3].Id, vm.SummarySelectionEndMessageId);
            Assert.Equal(3, vm.SummarySelectionMessageCount);
            Assert.True(vm.IsSummarySelectionValid);
            Assert.True(vm.MessageItems[2].IsInSummarySelectionRange);
        }

        [Fact]
        public async Task ReverseClickNormalizesRange()
        {
            var vm = await CreateInitializedViewModelWithMessagesAsync(5);

            vm.BeginSummarySelectionCommand.Execute(null);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[4]);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[1]);

            Assert.Equal(vm.MessageItems[1].Id, vm.SummarySelectionStartMessageId);
            Assert.Equal(vm.MessageItems[4].Id, vm.SummarySelectionEndMessageId);
            Assert.Equal(4, vm.SummarySelectionMessageCount);
        }

        [Fact]
        public async Task SelectingSameMessageTwiceCreatesSingleMessageRange()
        {
            var vm = await CreateInitializedViewModelWithMessagesAsync(3);

            vm.BeginSummarySelectionCommand.Execute(null);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[1]);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[1]);

            Assert.Equal(vm.MessageItems[1].Id, vm.SummarySelectionStartMessageId);
            Assert.Equal(vm.MessageItems[1].Id, vm.SummarySelectionEndMessageId);
            Assert.Equal(1, vm.SummarySelectionMessageCount);
            Assert.True(vm.IsSummarySelectionValid);
        }

        [Fact]
        public async Task ThirdClickRestartsSelectionWithNewAnchor()
        {
            var vm = await CreateInitializedViewModelWithMessagesAsync(5);

            vm.BeginSummarySelectionCommand.Execute(null);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[0]);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[2]);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[4]);

            Assert.Equal(vm.MessageItems[4].Id, vm.SummarySelectionStartMessageId);
            Assert.Null(vm.SummarySelectionEndMessageId);
            Assert.False(vm.MessageItems[0].IsInSummarySelectionRange);
            Assert.True(vm.MessageItems[4].IsSummarySelectionStart);
        }

        [Fact]
        public async Task ResetKeepsSelectionModeAndClearsRange()
        {
            var vm = await CreateInitializedViewModelWithMessagesAsync(3);
            vm.BeginSummarySelectionCommand.Execute(null);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[0]);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[2]);

            vm.ResetSummarySelectionCommand.Execute(null);

            Assert.True(vm.IsSummarySelectionMode);
            Assert.Null(vm.SummarySelectionStartMessageId);
            Assert.Null(vm.SummarySelectionEndMessageId);
            Assert.All(vm.MessageItems, item => Assert.False(item.IsInSummarySelectionRange));
        }

        [Fact]
        public async Task CancelExitsSelectionModeAndClearsState()
        {
            var vm = await CreateInitializedViewModelWithMessagesAsync(3);
            vm.BeginSummarySelectionCommand.Execute(null);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[0]);

            vm.CancelSummarySelectionCommand.Execute(null);

            Assert.False(vm.IsSummarySelectionMode);
            Assert.Null(vm.SummarySelectionStartMessageId);
            Assert.Empty(vm.SummarySelectionError);
            Assert.All(vm.MessageItems, item => Assert.False(item.IsInSummarySelectionRange));
        }

        [Fact]
        public async Task VisualStateMarksRangeStartAndEnd()
        {
            var vm = await CreateInitializedViewModelWithMessagesAsync(5);

            vm.BeginSummarySelectionCommand.Execute(null);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[1]);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[3]);

            Assert.False(vm.MessageItems[0].IsInSummarySelectionRange);
            Assert.True(vm.MessageItems[1].IsInSummarySelectionRange);
            Assert.True(vm.MessageItems[2].IsInSummarySelectionRange);
            Assert.True(vm.MessageItems[3].IsInSummarySelectionRange);
            Assert.False(vm.MessageItems[4].IsInSummarySelectionRange);
            Assert.True(vm.MessageItems[1].IsSummarySelectionStart);
            Assert.True(vm.MessageItems[3].IsSummarySelectionEnd);
        }

        [Fact]
        public async Task ValidRangeHasNoSelectionError()
        {
            var vm = await CreateInitializedViewModelWithMessagesAsync(3);

            vm.BeginSummarySelectionCommand.Execute(null);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[0]);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[2]);

            Assert.Empty(vm.SummarySelectionError);
            Assert.True(vm.IsSummarySelectionValid);
        }

        [Fact]
        public async Task OverlappingSummarySetsSelectionError()
        {
            var world = TestData.CreateWorld();
            AddMessages(world.ChatSessions[0], 5);
            world.ChatSessions[0].Summaries.Add(new ConversationSummary
            {
                StartMessageId = world.ChatSessions[0].Messages[1].Id,
                EndMessageId = world.ChatSessions[0].Messages[3].Id,
                Title = "기존 요약"
            });
            var vm = CreateViewModel(new WorldStore { ActiveWorldId = world.Id, Worlds = [world] });
            await vm.InitializeAsync();

            vm.BeginSummarySelectionCommand.Execute(null);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[0]);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[2]);

            Assert.Contains("겹칩니다", vm.SummarySelectionError);
            Assert.False(vm.IsSummarySelectionValid);
            Assert.True(vm.MessageItems[0].IsInSummarySelectionRange);
            Assert.True(vm.MessageItems[2].IsInSummarySelectionRange);
        }

        [Fact]
        public async Task AdjacentSummaryRangeIsValid()
        {
            var world = TestData.CreateWorld();
            AddMessages(world.ChatSessions[0], 5);
            world.ChatSessions[0].Summaries.Add(new ConversationSummary
            {
                StartMessageId = world.ChatSessions[0].Messages[0].Id,
                EndMessageId = world.ChatSessions[0].Messages[1].Id,
                Title = "기존 요약"
            });
            var vm = CreateViewModel(new WorldStore { ActiveWorldId = world.Id, Worlds = [world] });
            await vm.InitializeAsync();

            vm.BeginSummarySelectionCommand.Execute(null);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[2]);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[4]);

            Assert.Empty(vm.SummarySelectionError);
            Assert.True(vm.IsSummarySelectionValid);
        }

        [Fact]
        public async Task SendIsDisabledDuringSummarySelection()
        {
            var vm = await CreateInitializedViewModelWithMessagesAsync(1);
            vm.InputText = "보낼 메시지";

            vm.BeginSummarySelectionCommand.Execute(null);

            Assert.False(vm.SendMessageCommand.CanExecute(null));
        }

        [Fact]
        public async Task CancelReenablesSend()
        {
            var vm = await CreateInitializedViewModelWithMessagesAsync(1);
            vm.InputText = "보낼 메시지";
            vm.BeginSummarySelectionCommand.Execute(null);

            vm.CancelSummarySelectionCommand.Execute(null);

            Assert.True(vm.SendMessageCommand.CanExecute(null));
        }

        [Fact]
        public async Task SessionChangeClearsSummarySelection()
        {
            var world = TestData.CreateWorld();
            AddMessages(world.ChatSessions[0], 3);
            var other = new Character { Id = "char-2", Name = "다른 캐릭터" };
            world.Characters.Add(other);
            world.ChatSessions.Add(new ChatSession
            {
                WorldId = world.Id,
                CharacterId = other.Id,
                UserPersonaId = world.UserPersonas[0].Id,
                Messages = [new ChatMessage(ChatRole.User, "다른 대화")]
            });
            var vm = CreateViewModel(new WorldStore { ActiveWorldId = world.Id, Worlds = [world] });
            await vm.InitializeAsync();
            vm.BeginSummarySelectionCommand.Execute(null);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[0]);

            await vm.AddCharacterAsync(new Character { Id = "char-3", Name = "새 캐릭터" }, world.UserPersonas[0].Id);

            Assert.False(vm.IsSummarySelectionMode);
            Assert.Null(vm.SummarySelectionStartMessageId);
        }

        [Fact]
        public async Task ClearChatClearsSummarySelection()
        {
            var world = TestData.CreateWorld();
            AddMessages(world.ChatSessions[0], 3);
            var vm = CreateViewModel(new WorldStore { ActiveWorldId = world.Id, Worlds = [world] });
            await vm.InitializeAsync();
            vm.BeginSummarySelectionCommand.Execute(null);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[0]);

            await vm.ClearChatForCharacterAsync(world.Characters[0]);

            Assert.False(vm.IsSummarySelectionMode);
            Assert.Empty(vm.MessageItems);
            Assert.Null(vm.SummarySelectionStartMessageId);
        }

        [Fact]
        public async Task SummarySelectionDoesNotMutateDomain()
        {
            var world = TestData.CreateWorld();
            AddMessages(world.ChatSessions[0], 3);
            var session = world.ChatSessions[0];
            var messageSnapshot = session.Messages.ToList();
            var summarySnapshot = session.Summaries.ToList();
            var vm = CreateViewModel(new WorldStore { ActiveWorldId = world.Id, Worlds = [world] });
            await vm.InitializeAsync();

            vm.BeginSummarySelectionCommand.Execute(null);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[0]);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[2]);

            Assert.Equal(messageSnapshot, session.Messages);
            Assert.Equal(summarySnapshot, session.Summaries);
        }

        [Fact]
        public async Task InitializesConversationHistoryWarningState()
        {
            var world = TestData.CreateWorld();
            AddUserMessages(world.ChatSessions[0], 40);
            var vm = CreateViewModel(new WorldStore { ActiveWorldId = world.Id, Worlds = [world] });

            await vm.InitializeAsync();

            Assert.Equal(20, vm.UnsummarizedOldMessageCount);
            Assert.True(vm.HasUnsummarizedOldMessageWarning);
            Assert.Contains("20", vm.UnsummarizedOldMessageWarningText);
        }

        [Fact]
        public async Task SessionSwitchClearsStaleConversationHistoryWarning()
        {
            var world = TestData.CreateWorld();
            AddUserMessages(world.ChatSessions[0], 40);
            var vm = CreateViewModel(new WorldStore { ActiveWorldId = world.Id, Worlds = [world] });
            await vm.InitializeAsync();

            await vm.AddCharacterAsync(
                new Character { Id = "char-2", Name = "새 캐릭터" },
                world.UserPersonas[0].Id);

            Assert.Equal(0, vm.UnsummarizedOldMessageCount);
            Assert.False(vm.HasUnsummarizedOldMessageWarning);
        }

        [Fact]
        public async Task SendSuccessRefreshesConversationHistoryWarning()
        {
            var world = TestData.CreateWorld();
            AddUserMessages(world.ChatSessions[0], 39);
            var vm = CreateViewModel(new WorldStore { ActiveWorldId = world.Id, Worlds = [world] });
            await vm.InitializeAsync();
            vm.InputText = "새 메시지";

            await vm.SendMessageCommand.ExecuteAsync(null);

            Assert.True(vm.UnsummarizedOldMessageCount >= 20);
            Assert.True(vm.HasUnsummarizedOldMessageWarning);
        }

        [Fact]
        public async Task ClearChatSuccessClearsConversationHistoryWarning()
        {
            var world = TestData.CreateWorld();
            AddUserMessages(world.ChatSessions[0], 40);
            var vm = CreateViewModel(new WorldStore { ActiveWorldId = world.Id, Worlds = [world] });
            await vm.InitializeAsync();

            await vm.ClearChatForCharacterAsync(world.Characters[0]);

            Assert.Equal(0, vm.UnsummarizedOldMessageCount);
            Assert.False(vm.HasUnsummarizedOldMessageWarning);
        }

        [Fact]
        public async Task ClearChatSaveFailureRefreshesRolledBackConversationHistoryWarning()
        {
            var world = TestData.CreateWorld();
            AddUserMessages(world.ChatSessions[0], 40);
            var repository = new MemoryWorldRepository(new WorldStore { ActiveWorldId = world.Id, Worlds = [world] });
            var vm = CreateViewModel(repository);
            await vm.InitializeAsync();
            repository.SaveException = new InvalidOperationException("저장 실패");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => vm.ClearChatForCharacterAsync(world.Characters[0]));

            Assert.Equal(20, vm.UnsummarizedOldMessageCount);
            Assert.True(vm.HasUnsummarizedOldMessageWarning);
        }

        [Fact]
        public async Task SummaryPreviewSavedRefreshesConversationHistoryWarning()
        {
            var world = TestData.CreateWorld();
            AddUserMessages(world.ChatSessions[0], 40);
            var session = world.ChatSessions[0];
            var vm = CreateViewModel(new WorldStore { ActiveWorldId = world.Id, Worlds = [world] });
            await vm.InitializeAsync();
            vm.SummaryPreviewRequested += (_, e) =>
            {
                session.Summaries.Add(CreateSummary(session, 1, 20, "저장된 요약"));
                e.Result = SummaryPreviewResult.Saved;
            };

            vm.BeginSummarySelectionCommand.Execute(null);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[0]);
            vm.SelectSummaryMessageCommand.Execute(vm.MessageItems[19]);
            vm.OpenSummaryPreviewCommand.Execute(null);

            Assert.Equal(0, vm.UnsummarizedOldMessageCount);
            Assert.False(vm.HasUnsummarizedOldMessageWarning);
        }

        [Fact]
        public async Task SummaryManagementCloseRefreshesConversationHistoryWarning()
        {
            var world = TestData.CreateWorld();
            AddUserMessages(world.ChatSessions[0], 40);
            var session = world.ChatSessions[0];
            session.Summaries.Add(CreateSummary(session, 1, 20, "삭제될 요약"));
            var vm = CreateViewModel(new WorldStore { ActiveWorldId = world.Id, Worlds = [world] });
            await vm.InitializeAsync();
            vm.SummaryManagementRequested += (_, _) => session.Summaries.Clear();

            vm.OpenSummaryManagementCommand.Execute(null);

            Assert.Equal(20, vm.UnsummarizedOldMessageCount);
            Assert.True(vm.HasUnsummarizedOldMessageWarning);
        }

        [Fact]
        public async Task WarningDoesNotBlockSendCommand()
        {
            var world = TestData.CreateWorld();
            AddUserMessages(world.ChatSessions[0], 40);
            var vm = CreateViewModel(new WorldStore { ActiveWorldId = world.Id, Worlds = [world] });
            await vm.InitializeAsync();
            vm.InputText = "보낼 메시지";

            Assert.True(vm.HasUnsummarizedOldMessageWarning);
            Assert.True(vm.SendMessageCommand.CanExecute(null));
        }

        private static async Task<MainViewModel> CreateInitializedViewModelWithMessagesAsync(int messageCount)
        {
            var world = TestData.CreateWorld();
            AddMessages(world.ChatSessions[0], messageCount);
            var vm = CreateViewModel(new WorldStore { ActiveWorldId = world.Id, Worlds = [world] });
            await vm.InitializeAsync();
            return vm;
        }

        private static MainViewModel CreateViewModel(WorldStore store)
        {
            var repository = new MemoryWorldRepository(store);
            return CreateViewModel(repository);
        }

        private static MainViewModel CreateViewModel(WorldStore store, IChatModelClient chatClient)
        {
            var repository = new MemoryWorldRepository(store);
            return CreateViewModel(repository, chatClient);
        }

        private static MainViewModel CreateViewModel(MemoryWorldRepository repository, IChatModelClient? chatClient = null)
        {
            return new MainViewModel(
                repository,
                new MemorySettingsRepository(),
                new MemoryModelCatalog(),
                new ChatService(
                    chatClient ?? new FakeChatModelClient { Reply = "답" },
                    repository,
                    new ContextBuilder(
                        new PromptBuilder(),
                        new LoreMatcher(),
                        new RecentMessageSelector(),
                        new HistoricalContextBuilder())),
                new ConversationHistoryStatusService(
                    new RecentMessageSelector(),
                    new HistoricalContextBuilder()));
        }

        private static void AddMessages(ChatSession session, int count)
        {
            session.Messages.Clear();
            for (int i = 1; i <= count; i++)
                session.Messages.Add(new ChatMessage(i % 2 == 0 ? ChatRole.Assistant : ChatRole.User, $"message-{i}"));
        }

        private static void AddUserMessages(ChatSession session, int count)
        {
            session.Messages.Clear();
            for (int i = 1; i <= count; i++)
                session.Messages.Add(new ChatMessage(ChatRole.User, $"message-{i}"));
        }

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

        private class MemoryWorldRepository : IWorldRepository
        {
            private readonly WorldStore _store;
            public Exception? SaveException { get; set; }

            public MemoryWorldRepository(WorldStore store)
            {
                _store = store;
            }

            public Task<WorldStore> LoadAsync(System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(_store);
            public Task SaveAsync(WorldStore store, System.Threading.CancellationToken cancellationToken = default)
            {
                if (SaveException != null)
                    throw SaveException;

                return Task.CompletedTask;
            }
        }

        private class MemorySettingsRepository : ISettingsRepository
        {
            public Task<AppSettings> LoadAsync(System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(new AppSettings());
            public Task SaveAsync(AppSettings settings, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        private class MemoryModelCatalog : IChatModelCatalog
        {
            public System.Collections.Generic.IReadOnlyList<ChatModelOption> Models { get; } =
            [
                new ChatModelOption { Id = "model", Label = "Model" }
            ];
        }

        private class BlockingChatModelClient : IChatModelClient
        {
            private readonly TaskCompletionSource<string> _reply = new();
            public TaskCompletionSource<bool> CallStarted { get; } = new();

            public async Task<string> SendAsync(ChatCompletionRequest request, System.Threading.CancellationToken cancellationToken = default)
            {
                CallStarted.TrySetResult(true);
                return await _reply.Task;
            }

            public void Complete(string reply)
            {
                _reply.TrySetResult(reply);
            }
        }
    }
}
