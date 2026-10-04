using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AICharacterChat.Application.Chat;
using AICharacterChat.Application.Context;
using AICharacterChat.Application.Summaries;
using AICharacterChat.Domain.Models;
using AICharacterChat.Infrastructure.AI.Anthropic;
using AICharacterChat.Infrastructure.Persistence;
using AICharacterChat.Presentation.Models;
using AICharacterChat.Presentation.ViewModels;

namespace AICharacterChat
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly ConversationSummarizer _conversationSummarizer;
        private readonly ConversationSummaryPersistenceService _summaryPersistenceService;
        private bool _isScrollToBottomQueued;

        public MainWindow()
        {
            InitializeComponent();

            var paths = new AppDataPaths();
            var worldRepository = new JsonWorldRepository(paths, new LegacyDataMigrator());
            var settingsRepository = new JsonSettingsRepository(paths);
            var modelCatalog = new AnthropicModelCatalog();
            var chatClient = new AnthropicClient(new HttpClient());
            var promptBuilder = new PromptBuilder();
            var loreMatcher = new LoreMatcher();
            var recentMessageSelector = new RecentMessageSelector();
            var historicalContextBuilder = new HistoricalContextBuilder();
            var conversationHistoryStatusService = new ConversationHistoryStatusService(
                recentMessageSelector,
                historicalContextBuilder);
            var contextBuilder = new ContextBuilder(
                promptBuilder,
                loreMatcher,
                recentMessageSelector,
                historicalContextBuilder);
            var summaryService = new ConversationSummaryService();
            _conversationSummarizer = new ConversationSummarizer(chatClient, summaryService);
            _summaryPersistenceService = new ConversationSummaryPersistenceService(summaryService, worldRepository);
            var chatService = new ChatService(
                chatClient,
                worldRepository,
                contextBuilder);

            _viewModel = new MainViewModel(
                worldRepository,
                settingsRepository,
                modelCatalog,
                chatService,
                conversationHistoryStatusService,
                summaryService);
            _viewModel.MessageItems.CollectionChanged += (_, _) => QueueScrollToBottom();
            _viewModel.SummaryPreviewRequested += ViewModel_SummaryPreviewRequested;
            _viewModel.SummaryManagementRequested += ViewModel_SummaryManagementRequested;
            DataContext = _viewModel;
        }

        private void QueueScrollToBottom()
        {
            if (_isScrollToBottomQueued)
                return;

            _isScrollToBottomQueued = true;
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                _isScrollToBottomQueued = false;
                ChatScrollViewer.ScrollToBottom();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
        }

        private void ViewModel_SummaryPreviewRequested(object? sender, SummaryPreviewRequestedEventArgs e)
        {
            var viewModel = new SummaryPreviewViewModel(
                _conversationSummarizer,
                _summaryPersistenceService,
                e.Request);
            var window = new SummaryPreviewWindow(viewModel) { Owner = this };
            window.ShowDialog();
            e.Result = window.Result;
        }

        private void ViewModel_SummaryManagementRequested(object? sender, SummaryManagementRequestedEventArgs e)
        {
            var viewModel = new SummaryManagementViewModel(
                _summaryPersistenceService,
                _conversationSummarizer,
                e.Request);
            var window = new SummaryManagementWindow(viewModel) { Owner = this };
            window.ShowDialog();
        }

        private async void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
            {
                e.Handled = true;
                if (_viewModel.SendMessageCommand.CanExecute(null))
                    await _viewModel.SendMessageCommand.ExecuteAsync(null);
            }
        }

        private async void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await _viewModel.SaveSettingsAsync();
        }

        private async void AddWorldButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new WorldSettingsWindow(new World()) { Owner = this };
            if (win.ShowDialog() == true)
                await _viewModel.AddWorldAsync(win.ResultWorld);
        }

        private async void WorldEdit_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not World world)
                return;

            var win = new WorldSettingsWindow(world) { Owner = this };
            if (win.ShowDialog() == true)
                await _viewModel.UpdateWorldAsync(world, win.ResultWorld);
        }

        private void WorldDelete_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not World world)
                return;

            if (_viewModel.Worlds.Count <= 1)
            {
                MessageBox.Show("세계관이 한 개 이상 있어야 합니다.", "알림");
                return;
            }

            var confirm = MessageBox.Show(
                $"'{world.Name}'을(를) 삭제할까요?\n포함된 캐릭터와 대화 기록도 함께 삭제됩니다.",
                "세계관 삭제",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
                _viewModel.DeleteWorldCommand.Execute(world);
        }

        private async void AddCharacterButton_Click(object sender, RoutedEventArgs e)
        {
            var world = _viewModel.SelectedWorld;
            if (world == null)
                return;

            var win = new CharacterSettingsWindow(
                new Character(),
                world.Characters.ToList(),
                world,
                _viewModel.SaveAsync)
            {
                Owner = this
            };

            if (win.ShowDialog() == true)
                await _viewModel.AddCharacterAsync(win.ResultProfile, win.SelectedUserPersonaId);
        }

        private async void CharacterSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedWorld == null ||
                (sender as FrameworkElement)?.Tag is not Character character)
                return;

            var otherCharacters = _viewModel.SelectedWorld.Characters
                .Where(c => c.Id != character.Id)
                .ToList();

            var win = new CharacterSettingsWindow(
                character,
                otherCharacters,
                _viewModel.SelectedWorld,
                _viewModel.SaveAsync)
            {
                Owner = this
            };

            if (win.ShowDialog() == true)
                await _viewModel.UpdateCharacterAsync(character, win.ResultProfile, win.SelectedUserPersonaId);
        }

        private void CharacterLoreBook_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not Character character)
                return;

            var win = new LoreBookWindow(character, _viewModel.SaveAsync) { Owner = this };
            win.ShowDialog();
        }

        private async void CharacterClearHistory_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not Character character)
                return;

            var confirm = MessageBox.Show(
                $"'{character.Name}'과의 대화 기록을 초기화할까요?",
                "대화 기록 초기화",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
                await _viewModel.ClearChatForCharacterAsync(character);
        }

        private void CharacterDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedWorld == null ||
                (sender as FrameworkElement)?.Tag is not Character character)
                return;

            if (_viewModel.SelectedWorld.Characters.Count <= 1)
            {
                MessageBox.Show("캐릭터가 한 명 이상 있어야 합니다.", "알림");
                return;
            }

            var confirm = MessageBox.Show(
                $"'{character.Name}'을(를) 삭제할까요?\n대화 기록도 함께 삭제됩니다.",
                "캐릭터 삭제",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
                _viewModel.DeleteCharacterCommand.Execute(character);
        }
    }
}
