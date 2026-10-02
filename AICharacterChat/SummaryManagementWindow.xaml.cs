using System.ComponentModel;
using System.Windows;
using AICharacterChat.Presentation.Models;
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
            _viewModel.RegenerateConfirmationRequested += ViewModel_RegenerateConfirmationRequested;
            _viewModel.DeleteConfirmationRequested += ViewModel_DeleteConfirmationRequested;
            DataContext = _viewModel;
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            if (_viewModel.IsSaving || _viewModel.IsDeleting)
            {
                e.Cancel = true;
                MessageBox.Show("요약 변경사항을 저장하는 중입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
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

            if (!e.Cancel)
                _viewModel.OnWindowClosing();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ViewModel_RegenerateConfirmationRequested(
            object? sender,
            SummaryRegenerateConfirmationRequestedEventArgs e)
        {
            var result = MessageBox.Show(
                "현재 편집 중인 내용이 AI 재생성 결과로 대체됩니다.\n계속할까요?",
                "AI 다시 생성",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            e.Confirmed = result == MessageBoxResult.Yes;
        }

        private void ViewModel_DeleteConfirmationRequested(
            object? sender,
            SummaryDeleteConfirmationRequestedEventArgs e)
        {
            string message = e.HasUnsavedChanges
                ? "이 요약을 삭제할까요?\n\n저장하지 않은 수정 내용도 함께 사라집니다.\n원본 대화 메시지는 삭제되지 않습니다."
                : "이 요약을 삭제할까요?\n\n원본 대화 메시지는 삭제되지 않습니다.";

            var result = MessageBox.Show(
                message,
                "요약 삭제",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            e.Confirmed = result == MessageBoxResult.Yes;
        }
    }
}
