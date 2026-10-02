using AICharacterChat.Domain.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AICharacterChat.Presentation.ViewModels
{
    public sealed class SummaryManagementItemViewModel : ObservableObject
    {
        private string _title = "";
        private string _updatedAtText = "";

        public SummaryManagementItemViewModel(ConversationSummary summary)
        {
            Summary = summary;
            RefreshFromSummary();
        }

        public ConversationSummary Summary { get; }

        public string Title
        {
            get => _title;
            private set => SetProperty(ref _title, value);
        }

        public string UpdatedAtText
        {
            get => _updatedAtText;
            private set => SetProperty(ref _updatedAtText, value);
        }

        public void RefreshFromSummary()
        {
            Title = string.IsNullOrWhiteSpace(Summary.Title) ? "(제목 없음)" : Summary.Title;
            UpdatedAtText = $"수정 {Summary.UpdatedAt:yyyy-MM-dd HH:mm}";
        }
    }
}
