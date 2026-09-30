using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AICharacterChat.Application.Chat;
using AICharacterChat.Application.Interfaces;
using AICharacterChat.Application.Models;
using AICharacterChat.Application.Summaries;
using AICharacterChat.Domain.Enums;
using AICharacterChat.Domain.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AICharacterChat.Presentation.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly IWorldRepository _worldRepository;
        private readonly ISettingsRepository _settingsRepository;
        private readonly IChatModelCatalog _chatModelCatalog;
        private readonly ChatService _chatService;
        private WorldStore _store = new();
        private AppSettings _settings = new();
        private World? _selectedWorld;
        private Character? _selectedCharacter;
        private ChatSession? _selectedChatSession;
        private ChatModelOption? _selectedModel;
        private string _inputText = "";
        private bool _isSending;
        private string _errorMessage = "";
        private bool _isSummarySelectionMode;
        private ChatMessage? _summarySelectionAnchorMessage;
        private ChatMessage? _summarySelectionStartMessage;
        private ChatMessage? _summarySelectionEndMessage;
        private string _summarySelectionError = "";

        public ObservableCollection<World> Worlds { get; } = new();
        public ObservableCollection<Character> Characters { get; } = new();
        public ObservableCollection<ChatMessage> Messages { get; } = new();
        public ObservableCollection<ChatMessageItemViewModel> MessageItems { get; } = new();
        public ObservableCollection<ChatModelOption> Models { get; } = new();

        public ICommand SelectWorldCommand { get; }
        public ICommand SelectCharacterCommand { get; }
        public IAsyncRelayCommand SendMessageCommand { get; }
        public IAsyncRelayCommand SaveCommand { get; }
        public ICommand DeleteWorldCommand { get; }
        public ICommand DeleteCharacterCommand { get; }
        public ICommand ClearChatCommand { get; }
        public IRelayCommand BeginSummarySelectionCommand { get; }
        public IRelayCommand<ChatMessageItemViewModel> SelectSummaryMessageCommand { get; }
        public IRelayCommand ResetSummarySelectionCommand { get; }
        public IRelayCommand CancelSummarySelectionCommand { get; }

        public MainViewModel(
            IWorldRepository worldRepository,
            ISettingsRepository settingsRepository,
            IChatModelCatalog chatModelCatalog,
            ChatService chatService,
            ConversationSummaryService? summaryService = null)
        {
            _worldRepository = worldRepository;
            _settingsRepository = settingsRepository;
            _chatModelCatalog = chatModelCatalog;
            _chatService = chatService;
            _summaryService = summaryService ?? new ConversationSummaryService();

            SelectWorldCommand = new AsyncRelayCommand<World>(SelectWorldAsync);
            SelectCharacterCommand = new AsyncRelayCommand<Character>(SelectCharacterAsync);
            SendMessageCommand = new AsyncRelayCommand(SendMessageAsync, CanSendMessage);
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            DeleteWorldCommand = new AsyncRelayCommand<World>(DeleteWorldAsync);
            DeleteCharacterCommand = new AsyncRelayCommand<Character>(DeleteCharacterAsync);
            ClearChatCommand = new AsyncRelayCommand<Character>(ClearChatForCharacterAsync);
            BeginSummarySelectionCommand = new RelayCommand(BeginSummarySelection, CanBeginSummarySelection);
            SelectSummaryMessageCommand = new RelayCommand<ChatMessageItemViewModel>(SelectSummaryMessage);
            ResetSummarySelectionCommand = new RelayCommand(ResetSummarySelection);
            CancelSummarySelectionCommand = new RelayCommand(CancelSummarySelection);
        }

        private readonly ConversationSummaryService _summaryService;

        public WorldStore Store => _store;

        public World? SelectedWorld
        {
            get => _selectedWorld;
            private set
            {
                if (SetProperty(ref _selectedWorld, value))
                    OnPropertyChanged(nameof(HasSelectedWorld));
            }
        }

        public Character? SelectedCharacter
        {
            get => _selectedCharacter;
            private set
            {
                if (SetProperty(ref _selectedCharacter, value))
                    OnPropertyChanged(nameof(SelectedCharacterName));
            }
        }

        public ChatSession? SelectedChatSession
        {
            get => _selectedChatSession;
            private set => SetProperty(ref _selectedChatSession, value);
        }

        public ChatModelOption? SelectedModel
        {
            get => _selectedModel;
            set
            {
                if (SetProperty(ref _selectedModel, value) && value != null)
                    _settings.SelectedModel = value.Id;
            }
        }

        public string InputText
        {
            get => _inputText;
            set
            {
                if (SetProperty(ref _inputText, value))
                    SendMessageCommand.NotifyCanExecuteChanged();
            }
        }

        public bool IsSending
        {
            get => _isSending;
            private set
            {
                if (SetProperty(ref _isSending, value))
                {
                    SendMessageCommand.NotifyCanExecuteChanged();
                    BeginSummarySelectionCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        public bool HasSelectedWorld => SelectedWorld != null;
        public string SelectedCharacterName => SelectedCharacter?.Name ?? "";
        public bool HasMessages => SelectedChatSession?.Messages.Count > 0;

        public bool IsSummarySelectionMode
        {
            get => _isSummarySelectionMode;
            private set
            {
                if (SetProperty(ref _isSummarySelectionMode, value))
                {
                    OnPropertyChanged(nameof(SummarySelectionStatusText));
                    OnPropertyChanged(nameof(CanShowBeginSummarySelection));
                    SendMessageCommand.NotifyCanExecuteChanged();
                    BeginSummarySelectionCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool CanShowBeginSummarySelection => !IsSummarySelectionMode;
        public Guid? SummarySelectionStartMessageId => _summarySelectionStartMessage?.Id;
        public Guid? SummarySelectionEndMessageId => _summarySelectionEndMessage?.Id;

        public int SummarySelectionMessageCount
        {
            get
            {
                if (SelectedChatSession == null ||
                    _summarySelectionStartMessage == null ||
                    _summarySelectionEndMessage == null)
                    return 0;

                int startIndex = FindMessageReferenceIndex(_summarySelectionStartMessage);
                int endIndex = FindMessageReferenceIndex(_summarySelectionEndMessage);
                if (startIndex < 0 || endIndex < 0)
                    return 0;

                return Math.Abs(endIndex - startIndex) + 1;
            }
        }

        public bool HasSummarySelectionRange =>
            IsSummarySelectionMode &&
            _summarySelectionStartMessage != null &&
            _summarySelectionEndMessage != null;

        public bool IsSummarySelectionValid =>
            HasSummarySelectionRange &&
            string.IsNullOrWhiteSpace(SummarySelectionError);

        public bool HasSummarySelectionError => !string.IsNullOrWhiteSpace(SummarySelectionError);

        public string SummarySelectionError
        {
            get => _summarySelectionError;
            private set
            {
                if (SetProperty(ref _summarySelectionError, value))
                {
                    OnPropertyChanged(nameof(HasSummarySelectionError));
                    OnPropertyChanged(nameof(IsSummarySelectionValid));
                }
            }
        }

        public string SummarySelectionStatusText
        {
            get
            {
                if (!IsSummarySelectionMode)
                    return "";

                if (_summarySelectionAnchorMessage == null)
                    return "시작 메시지를 선택하세요";

                if (_summarySelectionEndMessage == null)
                    return "끝 메시지를 선택하세요";

                return $"{SummarySelectionMessageCount}개 메시지 선택됨";
            }
        }

        public async Task InitializeAsync()
        {
            _store = await _worldRepository.LoadAsync();
            _settings = await _settingsRepository.LoadAsync();

            Models.Clear();
            foreach (var model in _chatModelCatalog.Models)
                Models.Add(model);

            Worlds.Clear();
            foreach (var world in _store.Worlds)
                Worlds.Add(world);

            SelectedModel = Models.FirstOrDefault(m => m.Id == _settings.SelectedModel)
                            ?? Models.FirstOrDefault();
            await SelectWorldAsync(_store.ActiveWorld ?? Worlds.FirstOrDefault());
        }

        public async Task SaveAsync()
        {
            try
            {
                await _worldRepository.SaveAsync(_store);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                throw;
            }
        }

        public async Task SaveSettingsAsync()
        {
            try
            {
                await _settingsRepository.SaveAsync(_settings);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                throw;
            }
        }

        public async Task AddWorldAsync(World world)
        {
            _store.Worlds.Add(world);
            Worlds.Add(world);
            await SelectWorldAsync(world);
        }

        public async Task UpdateWorldAsync(World target, World source)
        {
            target.Name = source.Name;
            target.Genre = source.Genre;
            target.Era = source.Era;
            target.Description = source.Description;
            target.Rules = source.Rules;
            RefreshWorlds();
            await SelectWorldAsync(target);
        }

        public async Task AddCharacterAsync(Character character, string? userPersonaId)
        {
            if (SelectedWorld == null)
                return;

            SelectedWorld.Characters.Add(character);
            SelectedWorld.ActiveCharacterId = character.Id;
            EnsureSession(SelectedWorld, character, userPersonaId);
            RefreshCharacters();
            await SelectCharacterAsync(character);
        }

        public async Task UpdateCharacterAsync(Character target, Character source, string? userPersonaId)
        {
            target.Name = source.Name;
            target.Age = source.Age;
            target.Gender = source.Gender;
            target.Job = source.Job;
            target.Appearance = source.Appearance;
            target.Personality = source.Personality;
            target.Relationships = source.Relationships;
            target.Etc = source.Etc;
            target.Secret = source.Secret;
            target.SpeechStyle = source.SpeechStyle;
            target.DefaultScenario = source.DefaultScenario;
            target.CustomFields = source.CustomFields;
            var session = SelectedWorld?.GetSessionForCharacter(target.Id);
            if (session != null)
                session.UserPersonaId = ResolveUserPersonaId(SelectedWorld!, userPersonaId);
            RefreshCharacters();
            await SelectCharacterAsync(target);
        }

        private async Task SendMessageAsync()
        {
            if (SelectedWorld == null ||
                SelectedCharacter == null ||
                SelectedChatSession == null ||
                SelectedModel == null)
                return;

            string input = InputText;
            InputText = "";
            ErrorMessage = "";
            IsSending = true;
            RefreshMessages();

            var result = await _chatService.SendAsync(
                _store,
                SelectedWorld,
                SelectedCharacter,
                SelectedChatSession,
                input,
                SelectedModel.Id);

            IsSending = false;
            if (!result.IsSuccess)
                ErrorMessage = result.ErrorMessage ?? "응답을 가져오지 못했습니다.";

            RefreshMessages();
        }

        private bool CanSendMessage()
        {
            return !IsSummarySelectionMode &&
                   !IsSending &&
                   SelectedChatSession != null &&
                   !string.IsNullOrWhiteSpace(InputText);
        }

        private async Task SelectWorldAsync(World? world)
        {
            if (world == null)
                return;

            SelectedWorld = world;
            _store.ActiveWorldId = world.Id;
            RefreshCharacters();
            await SelectCharacterAsync(world.ActiveCharacter ?? Characters.FirstOrDefault(), save: false);
            await SaveAsync();
        }

        private async Task SelectCharacterAsync(Character? character)
        {
            await SelectCharacterAsync(character, save: true);
        }

        private async Task SelectCharacterAsync(Character? character, bool save)
        {
            if (SelectedWorld == null || character == null)
            {
                CancelSummarySelection();
                SelectedCharacter = null;
                SelectedChatSession = null;
                Messages.Clear();
                MessageItems.Clear();
                return;
            }

            CancelSummarySelection();
            SelectedCharacter = character;
            SelectedWorld.ActiveCharacterId = character.Id;
            SelectedChatSession = EnsureSession(SelectedWorld, character);
            RefreshMessages();
            if (save)
                await SaveAsync();
        }

        private async Task DeleteWorldAsync(World? world)
        {
            if (world == null || Worlds.Count <= 1)
                return;

            _store.Worlds.Remove(world);
            Worlds.Remove(world);
            await SelectWorldAsync(Worlds.FirstOrDefault());
        }

        private async Task DeleteCharacterAsync(Character? character)
        {
            if (SelectedWorld == null || character == null || SelectedWorld.Characters.Count <= 1)
                return;

            SelectedWorld.Characters.Remove(character);
            SelectedWorld.ChatSessions.RemoveAll(s => s.CharacterId == character.Id);
            RefreshCharacters();
            await SelectCharacterAsync(SelectedWorld.Characters.FirstOrDefault());
        }

        public async Task ClearChatForCharacterAsync(Character? character)
        {
            if (SelectedWorld == null || character == null)
                return;

            var session = SelectedWorld.GetSessionForCharacter(character.Id);
            if (session == null)
                return;

            var originalMessages = session.Messages.ToList();
            var originalSummaries = session.Summaries.ToList();
            if (ReferenceEquals(session, SelectedChatSession))
                CancelSummarySelection();

            try
            {
                session.Messages.Clear();
                session.Summaries.Clear();
                RefreshMessages();
                await SaveAsync();
            }
            catch
            {
                session.Messages = originalMessages;
                session.Summaries = originalSummaries;
                RefreshMessages();
                throw;
            }
        }

        private ChatSession EnsureSession(World world, Character character, string? requestedUserPersonaId = null)
        {
            var session = world.GetSessionForCharacter(character.Id);
            if (session != null)
                return session;

            string userPersonaId = ResolveUserPersonaId(world, requestedUserPersonaId);
            session = new ChatSession
            {
                WorldId = world.Id,
                CharacterId = character.Id,
                UserPersonaId = userPersonaId
            };
            world.ChatSessions.Add(session);
            return session;
        }

        private static string ResolveUserPersonaId(World world, string? requestedUserPersonaId)
        {
            return world.UserPersonas.Any(u => u.Id == requestedUserPersonaId)
                ? requestedUserPersonaId!
                : world.UserPersonas.FirstOrDefault()?.Id ?? "";
        }

        private void RefreshWorlds()
        {
            Worlds.Clear();
            foreach (var world in _store.Worlds)
                Worlds.Add(world);
        }

        private void RefreshCharacters()
        {
            Characters.Clear();
            if (SelectedWorld == null)
                return;

            foreach (var character in SelectedWorld.Characters)
                Characters.Add(character);
        }

        public void RefreshMessages()
        {
            Messages.Clear();
            MessageItems.Clear();
            if (SelectedChatSession == null)
            {
                NotifyMessageStateChanged();
                return;
            }

            foreach (var message in SelectedChatSession.Messages)
            {
                Messages.Add(message);
                MessageItems.Add(new ChatMessageItemViewModel(message));
            }

            UpdateSummarySelectionVisuals();
            NotifyMessageStateChanged();
        }

        private void BeginSummarySelection()
        {
            if (!CanBeginSummarySelection())
                return;

            ClearSummarySelection(exitMode: false);
            IsSummarySelectionMode = true;
            NotifySelectionStateChanged();
            UpdateSummarySelectionVisuals();
        }

        private bool CanBeginSummarySelection()
        {
            return !IsSending &&
                   SelectedChatSession != null &&
                   SelectedChatSession.Messages.Count > 0 &&
                   !IsSummarySelectionMode;
        }

        private void SelectSummaryMessage(ChatMessageItemViewModel? item)
        {
            if (!IsSummarySelectionMode || item == null || SelectedChatSession == null)
                return;

            var clickedMessage = item.Message;
            int clickedIndex = FindMessageReferenceIndex(clickedMessage);
            if (clickedIndex < 0)
            {
                SummarySelectionError = "선택한 메시지를 현재 대화에서 찾을 수 없습니다.";
                NotifySelectionStateChanged();
                UpdateSummarySelectionVisuals();
                return;
            }

            if (_summarySelectionAnchorMessage == null || _summarySelectionEndMessage != null)
            {
                _summarySelectionAnchorMessage = clickedMessage;
                _summarySelectionStartMessage = clickedMessage;
                _summarySelectionEndMessage = null;
                SummarySelectionError = "";
                NotifySelectionStateChanged();
                UpdateSummarySelectionVisuals();
                return;
            }

            int anchorIndex = FindMessageReferenceIndex(_summarySelectionAnchorMessage);
            if (anchorIndex < 0)
            {
                SummarySelectionError = "선택한 메시지를 현재 대화에서 찾을 수 없습니다.";
                NotifySelectionStateChanged();
                UpdateSummarySelectionVisuals();
                return;
            }

            int startIndex = Math.Min(anchorIndex, clickedIndex);
            int endIndex = Math.Max(anchorIndex, clickedIndex);
            _summarySelectionStartMessage = SelectedChatSession.Messages[startIndex];
            _summarySelectionEndMessage = SelectedChatSession.Messages[endIndex];

            var validation = _summaryService.ValidateRange(
                SelectedChatSession,
                _summarySelectionStartMessage.Id,
                _summarySelectionEndMessage.Id);
            SummarySelectionError = validation.IsSuccess
                ? ""
                : validation.ErrorMessage ?? "요약 범위가 올바르지 않습니다.";

            NotifySelectionStateChanged();
            UpdateSummarySelectionVisuals();
        }

        private void ResetSummarySelection()
        {
            if (!IsSummarySelectionMode)
                return;

            ClearSummarySelection(exitMode: false);
            NotifySelectionStateChanged();
            UpdateSummarySelectionVisuals();
        }

        private void CancelSummarySelection()
        {
            ClearSummarySelection(exitMode: true);
            NotifySelectionStateChanged();
            UpdateSummarySelectionVisuals();
        }

        private void ClearSummarySelection(bool exitMode)
        {
            _summarySelectionAnchorMessage = null;
            _summarySelectionStartMessage = null;
            _summarySelectionEndMessage = null;
            SummarySelectionError = "";

            if (exitMode)
                IsSummarySelectionMode = false;
        }

        private void UpdateSummarySelectionVisuals()
        {
            int startIndex = _summarySelectionStartMessage == null
                ? -1
                : FindMessageReferenceIndex(_summarySelectionStartMessage);
            int endIndex = _summarySelectionEndMessage == null
                ? startIndex
                : FindMessageReferenceIndex(_summarySelectionEndMessage);

            if (startIndex > endIndex)
                (startIndex, endIndex) = (endIndex, startIndex);

            for (int i = 0; i < MessageItems.Count; i++)
            {
                bool inRange = IsSummarySelectionMode &&
                               startIndex >= 0 &&
                               endIndex >= 0 &&
                               i >= startIndex &&
                               i <= endIndex;
                MessageItems[i].IsInSummarySelectionRange = inRange;
                MessageItems[i].IsSummarySelectionStart = IsSummarySelectionMode && i == startIndex;
                MessageItems[i].IsSummarySelectionEnd = IsSummarySelectionMode && _summarySelectionEndMessage != null && i == endIndex;
            }
        }

        private int FindMessageReferenceIndex(ChatMessage message)
        {
            if (SelectedChatSession == null)
                return -1;

            for (int i = 0; i < SelectedChatSession.Messages.Count; i++)
            {
                if (ReferenceEquals(SelectedChatSession.Messages[i], message))
                    return i;
            }

            return -1;
        }

        private void NotifySelectionStateChanged()
        {
            OnPropertyChanged(nameof(SummarySelectionStartMessageId));
            OnPropertyChanged(nameof(SummarySelectionEndMessageId));
            OnPropertyChanged(nameof(SummarySelectionMessageCount));
            OnPropertyChanged(nameof(HasSummarySelectionRange));
            OnPropertyChanged(nameof(IsSummarySelectionValid));
            OnPropertyChanged(nameof(SummarySelectionStatusText));
        }

        private void NotifyMessageStateChanged()
        {
            OnPropertyChanged(nameof(HasMessages));
            BeginSummarySelectionCommand.NotifyCanExecuteChanged();
        }
    }
}
