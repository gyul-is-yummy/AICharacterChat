using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AICharacterChat.Application.Chat;
using AICharacterChat.Application.Interfaces;
using AICharacterChat.Application.Models;
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

        public ObservableCollection<World> Worlds { get; } = new();
        public ObservableCollection<Character> Characters { get; } = new();
        public ObservableCollection<ChatMessage> Messages { get; } = new();
        public ObservableCollection<ChatModelOption> Models { get; } = new();

        public ICommand SelectWorldCommand { get; }
        public ICommand SelectCharacterCommand { get; }
        public IAsyncRelayCommand SendMessageCommand { get; }
        public IAsyncRelayCommand SaveCommand { get; }
        public ICommand DeleteWorldCommand { get; }
        public ICommand DeleteCharacterCommand { get; }
        public ICommand ClearChatCommand { get; }

        public MainViewModel(
            IWorldRepository worldRepository,
            ISettingsRepository settingsRepository,
            IChatModelCatalog chatModelCatalog,
            ChatService chatService)
        {
            _worldRepository = worldRepository;
            _settingsRepository = settingsRepository;
            _chatModelCatalog = chatModelCatalog;
            _chatService = chatService;

            SelectWorldCommand = new AsyncRelayCommand<World>(SelectWorldAsync);
            SelectCharacterCommand = new AsyncRelayCommand<Character>(SelectCharacterAsync);
            SendMessageCommand = new AsyncRelayCommand(SendMessageAsync, CanSendMessage);
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            DeleteWorldCommand = new AsyncRelayCommand<World>(DeleteWorldAsync);
            DeleteCharacterCommand = new AsyncRelayCommand<Character>(DeleteCharacterAsync);
            ClearChatCommand = new AsyncRelayCommand<Character>(ClearChatForCharacterAsync);
        }

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
                    SendMessageCommand.NotifyCanExecuteChanged();
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        public bool HasSelectedWorld => SelectedWorld != null;
        public string SelectedCharacterName => SelectedCharacter?.Name ?? "";

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
            return !IsSending &&
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
                SelectedCharacter = null;
                SelectedChatSession = null;
                Messages.Clear();
                return;
            }

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

            session.Messages.Clear();
            RefreshMessages();
            await SaveAsync();
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
            if (SelectedChatSession == null)
                return;

            foreach (var message in SelectedChatSession.Messages)
                Messages.Add(message);
        }
    }
}
