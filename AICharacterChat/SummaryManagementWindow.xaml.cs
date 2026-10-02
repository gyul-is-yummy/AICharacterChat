using System.ComponentModel;
using System.Windows;
using AICharacterChat.Presentation.ViewModels;

namespace AICharacterChat
{
    public partial class SummaryManagementWindow : Window
    {
        private readonly SummaryManagementViewModel _viewModel;

        public SummaryManagementWindow(SummaryManagementViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (_viewModel.IsSaving)
            {
                e.Cancel = true;
                MessageBox.Show("요약을 저장하는 중입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_viewModel.IsDirty)
            {
                var result = MessageBox.Show(
                    "저장하지 않은 변경사항을 버리고 닫을까요?",
                    "변경사항 버리기",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    e.Cancel = true;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
