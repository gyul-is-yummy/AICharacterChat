using System;
using System.Threading;
using System.Threading.Tasks;
using AICharacterChat.Application.Models;
using AICharacterChat.Application.Summaries;
using AICharacterChat.Presentation.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AICharacterChat.Presentation.ViewModels
{
    public sealed class SummaryPreviewViewModel : ObservableObject
    {
        private readonly ConversationSummarizer _summarizer;
        private readonly ConversationSummaryPersistenceService _persistenceService;
        private readonly SummaryPreviewRequest _request;
        private CancellationTokenSource? _generationCancellation;
        private bool _isInitialized;
        private bool _isClosingOrClosed;
        private bool _isGenerating;
        private bool _isSaving;
        private bool _hasDraft;
        private string _errorMessage = "";
        private string _title = "";
        private string _currentSituation = "";
        private string _keyEvents = "";
        private string _relationshipChanges = "";
        private string _promisesAndImportantStatements = "";
        private string _unresolvedMatters = "";
        private string _persistentState = "";

        public SummaryPreviewViewModel(
            ConversationSummarizer summarizer,
            ConversationSummaryPersistenceService persistenceService,
            SummaryPreviewRequest request)
        {
            _summarizer = summarizer;
            _persistenceService = persistenceService;
            _request = request;

            RegenerateCommand = new AsyncRelayCommand(RegenerateAsync, CanRegenerate);
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
            CancelCommand = new RelayCommand(Cancel, CanCancel);
        }

        public event EventHandler<SummaryPreviewCloseRequestedEventArgs>? CloseRequested;

        public IAsyncRelayCommand RegenerateCommand { get; }
        public IAsyncRelayCommand SaveCommand { get; }
        public IRelayCommand CancelCommand { get; }

        public Guid StartMessageId => _request.StartMessageId;
        public Guid EndMessageId => _request.EndMessageId;
        public string ModelId => _request.ModelId;

        public bool IsGenerating
        {
            get => _isGenerating;
            private set
            {
                if (SetProperty(ref _isGenerating, value))
                    NotifyBusyStateChanged();
            }
        }

        public bool IsSaving
        {
            get => _isSaving;
            private set
            {
                if (SetProperty(ref _isSaving, value))
                {
                    NotifyBusyStateChanged();
                    CancelCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool IsBusy => IsGenerating || IsSaving;

        public bool HasDraft
        {
            get => _hasDraft;
            private set
            {
                if (SetProperty(ref _hasDraft, value))
                {
                    OnPropertyChanged(nameof(RegenerateButtonText));
                    SaveCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                if (SetProperty(ref _errorMessage, value))
                    OnPropertyChanged(nameof(HasErrorMessage));
            }
        }

        public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

        public string Title
        {
            get => _title;
            set
            {
                if (SetProperty(ref _title, value))
                {
                    OnPropertyChanged(nameof(TitleLengthText));
                    OnPropertyChanged(nameof(IsTitleValid));
                    SaveCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public string CurrentSituation
        {
            get => _currentSituation;
            set => SetProperty(ref _currentSituation, value);
        }

        public string KeyEvents
        {
            get => _keyEvents;
            set => SetProperty(ref _keyEvents, value);
        }

        public string RelationshipChanges
        {
            get => _relationshipChanges;
            set => SetProperty(ref _relationshipChanges, value);
        }

        public string PromisesAndImportantStatements
        {
            get => _promisesAndImportantStatements;
            set => SetProperty(ref _promisesAndImportantStatements, value);
        }

        public string UnresolvedMatters
        {
            get => _unresolvedMatters;
            set => SetProperty(ref _unresolvedMatters, value);
        }

        public string PersistentState
        {
            get => _persistentState;
            set => SetProperty(ref _persistentState, value);
        }

        public string TitleLengthText => $"{Title.Trim().Length} / {ConversationSummaryService.MaxTitleLength}";

        public bool IsTitleValid =>
            !string.IsNullOrWhiteSpace(Title) &&
            Title.Trim().Length <= ConversationSummaryService.MaxTitleLength;

        public string RegenerateButtonText => HasDraft ? "다시 생성" : "다시 시도";

        public async Task InitializeAsync()
        {
            if (_isInitialized || _isClosingOrClosed)
                return;

            _isInitialized = true;
            await GenerateDraftAsync(replaceExistingFields: true);
        }

        public void CancelActiveGeneration()
        {
            _generationCancellation?.Cancel();
        }

        public void OnWindowClosing()
        {
            _isClosingOrClosed = true;
            CancelActiveGeneration();
        }

        private async Task RegenerateAsync()
        {
            if (!CanRegenerate())
                return;

            await GenerateDraftAsync(replaceExistingFields: true);
        }

        private bool CanRegenerate() => !_isClosingOrClosed && !IsBusy;

        private async Task GenerateDraftAsync(bool replaceExistingFields)
        {
            if (_isClosingOrClosed)
                return;

            var cancellation = CreateGenerationCancellation();
            IsGenerating = true;
            ErrorMessage = "";

            try
            {
                var result = await _summarizer.GenerateNewDraftAsync(
                    _request.Session,
                    _request.Character,
                    _request.StartMessageId,
                    _request.EndMessageId,
                    _request.ModelId,
                    cancellation.Token);

                if (!IsCurrentActiveGeneration(cancellation))
                    return;

                if (result.IsCanceled)
                    return;

                if (!result.IsSuccess || result.Draft == null)
                {
                    ErrorMessage = result.ErrorMessage ?? "요약 생성에 실패했습니다.";
                    return;
                }

                if (replaceExistingFields)
                    ApplyDraft(result.Draft);

                HasDraft = true;
                ErrorMessage = "";
            }
            finally
            {
                bool isCurrentOperation = ReferenceEquals(_generationCancellation, cancellation);
                if (isCurrentOperation)
                {
                    _generationCancellation = null;
                    cancellation.Dispose();
                }

                if (!_isClosingOrClosed && isCurrentOperation)
                    IsGenerating = false;
            }
        }

        private async Task SaveAsync()
        {
            if (!CanSave())
                return;

            IsSaving = true;
            ErrorMessage = "";

            try
            {
                var result = await _persistenceService.AddAsync(
                    _request.Store,
                    _request.Session,
                    CreateDraftFromEditableFields());

                if (result.IsSuccess)
                {
                    IsSaving = false;
                    CloseRequested?.Invoke(
                        this,
                        new SummaryPreviewCloseRequestedEventArgs(SummaryPreviewResult.Saved));
                    return;
                }

                ErrorMessage = result.ErrorMessage ?? "요약 저장에 실패했습니다.";
            }
            finally
            {
                if (IsSaving)
                    IsSaving = false;
            }
        }

        private bool CanSave() => !_isClosingOrClosed && HasDraft && !IsBusy && IsTitleValid;

        private void Cancel()
        {
            if (!CanCancel())
                return;

            CancelActiveGeneration();
            CloseRequested?.Invoke(
                this,
                new SummaryPreviewCloseRequestedEventArgs(SummaryPreviewResult.Canceled));
        }

        private bool CanCancel() => !IsSaving;

        private CancellationTokenSource CreateGenerationCancellation()
        {
            _generationCancellation?.Cancel();
            _generationCancellation?.Dispose();
            _generationCancellation = new CancellationTokenSource();
            return _generationCancellation;
        }

        private bool IsCurrentActiveGeneration(CancellationTokenSource cancellation)
        {
            return !_isClosingOrClosed &&
                   !cancellation.IsCancellationRequested &&
                   ReferenceEquals(_generationCancellation, cancellation);
        }

        private void ApplyDraft(SummaryDraft draft)
        {
            Title = draft.Title;
            CurrentSituation = draft.CurrentSituation;
            KeyEvents = draft.KeyEvents;
            RelationshipChanges = draft.RelationshipChanges;
            PromisesAndImportantStatements = draft.PromisesAndImportantStatements;
            UnresolvedMatters = draft.UnresolvedMatters;
            PersistentState = draft.PersistentState;
        }

        private SummaryDraft CreateDraftFromEditableFields() =>
            new()
            {
                StartMessageId = _request.StartMessageId,
                EndMessageId = _request.EndMessageId,
                Title = Title,
                CurrentSituation = CurrentSituation,
                KeyEvents = KeyEvents,
                RelationshipChanges = RelationshipChanges,
                PromisesAndImportantStatements = PromisesAndImportantStatements,
                UnresolvedMatters = UnresolvedMatters,
                PersistentState = PersistentState
            };

        private void NotifyBusyStateChanged()
        {
            OnPropertyChanged(nameof(IsBusy));
            RegenerateCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
        }
    }
}
