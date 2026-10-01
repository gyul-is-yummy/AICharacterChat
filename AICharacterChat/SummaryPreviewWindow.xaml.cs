using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using AICharacterChat.Presentation.Models;
using AICharacterChat.Presentation.ViewModels;

namespace AICharacterChat
{
    public partial class SummaryPreviewWindow : Window
    {
        private readonly SummaryPreviewViewModel _viewModel;
        private bool _initialized;

        public SummaryPreviewWindow(SummaryPreviewViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _viewModel.CloseRequested += ViewModel_CloseRequested;
            DataContext = _viewModel;
        }

        public SummaryPreviewResult Result { get; private set; } = SummaryPreviewResult.Canceled;

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized)
                return;

            _initialized = true;
            await _viewModel.InitializeAsync();
        }

        private void ViewModel_CloseRequested(object? sender, SummaryPreviewCloseRequestedEventArgs e)
        {
            Result = e.Result;
            DialogResult = e.Result == SummaryPreviewResult.Saved;
            Close();
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (_viewModel.IsSaving)
            {
                e.Cancel = true;
                MessageBox.Show("요약을 저장하는 중입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _viewModel.OnWindowClosing();
        }
    }
}
