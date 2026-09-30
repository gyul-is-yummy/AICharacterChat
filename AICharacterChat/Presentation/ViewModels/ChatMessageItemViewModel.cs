using System;
using AICharacterChat.Domain.Enums;
using AICharacterChat.Domain.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AICharacterChat.Presentation.ViewModels
{
    public sealed class ChatMessageItemViewModel : ObservableObject
    {
        private bool _isInSummarySelectionRange;
        private bool _isSummarySelectionStart;
        private bool _isSummarySelectionEnd;

        public ChatMessageItemViewModel(ChatMessage message)
        {
            Message = message;
        }

        public ChatMessage Message { get; }
        public Guid Id => Message.Id;
        public ChatRole Role => Message.Role;
        public string Content => Message.Content;
        public DateTimeOffset CreatedAt => Message.CreatedAt;

        public bool IsInSummarySelectionRange
        {
            get => _isInSummarySelectionRange;
            set => SetProperty(ref _isInSummarySelectionRange, value);
        }

        public bool IsSummarySelectionStart
        {
            get => _isSummarySelectionStart;
            set => SetProperty(ref _isSummarySelectionStart, value);
        }

        public bool IsSummarySelectionEnd
        {
            get => _isSummarySelectionEnd;
            set => SetProperty(ref _isSummarySelectionEnd, value);
        }
    }
}
