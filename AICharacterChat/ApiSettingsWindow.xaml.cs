using System.ComponentModel;
using System.Windows;
using AICharacterChat.Presentation.ViewModels;

namespace AICharacterChat
{
    public partial class ApiSettingsWindow : Window
    {
        private readonly ApiSettingsViewModel _viewModel;
        private bool _initialized;

        public ApiSettingsWindow(ApiSettingsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized)
                return;

            _initialized = true;
            await _viewModel.InitializeAsync();
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            bool saved = await _viewModel.SaveAsync(ApiKeyPasswordBox.Password);
            if (saved)
                ApiKeyPasswordBox.Clear();
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "저장된 Anthropic API Key를 삭제할까요?",
                "API Key 삭제",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            bool deleted = await _viewModel.DeleteAsync();
            if (deleted)
                ApiKeyPasswordBox.Clear();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (!_viewModel.IsBusy)
                return;

            e.Cancel = true;
            MessageBox.Show("API Key 작업을 처리하는 중입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
