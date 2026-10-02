using System.Collections.ObjectModel;
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
    public sealed class SummaryManagementViewModel : ObservableObject
    {
        private readonly ConversationSummaryPersistenceService _persistenceService;
        private readonly ConversationSummarizer _summarizer;
        private readonly SummaryManagementRequest _request;
        private readonly ConversationSummaryService _summaryService = new();
        private CancellationTokenSource? _regenerationCancellation;
        private SummaryManagementItemViewModel? _selectedItem;
        private bool _isClosingOrClosed;
        private bool _isSaving;
        private bool _isDeleting;
        private bool _isRegenerating;
        private string _errorMessage = "";
        private string _editTitle = "";
        private string _editCurrentSituation = "";
        private string _editKeyEvents = "";
        private string _editRelationshipChanges = "";
        private string _editPromisesAndImportantStatements = "";
        private string _editUnresolvedMatters = "";
        private string _editPersistentState = "";

        public SummaryManagementViewModel(
            ConversationSummaryPersistenceService persistenceService,
            ConversationSummarizer summarizer,
            SummaryManagementRequest request)
        {
            _persistenceService = persistenceService;
            _summarizer = summarizer;
            _request = request;

            SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
            RegenerateCommand = new AsyncRelayCommand(RegenerateAsync, CanRegenerate);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync, CanDelete);
            DiscardChangesCommand = new RelayCommand(DiscardChanges, CanDiscardChanges);

            foreach (var summary in request.Session.Summaries)
                Summaries.Add(new SummaryManagementItemViewModel(summary));

            if (Summaries.Count > 0)
                SelectItem(Summaries[0]);
        }

        public ObservableCollection<SummaryManagementItemViewModel> Summaries { get; } = new();

        public IAsyncRelayCommand SaveCommand { get; }
        public IAsyncRelayCommand RegenerateCommand { get; }
        public IAsyncRelayCommand DeleteCommand { get; }
        public IRelayCommand DiscardChangesCommand { get; }

        public event EventHandler<SummaryRegenerateConfirmationRequestedEventArgs>? RegenerateConfirmationRequested;
        public event EventHandler<SummaryDeleteConfirmationRequestedEventArgs>? DeleteConfirmationRequested;

        public SummaryManagementItemViewModel? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (ReferenceEquals(_selectedItem, value))
                    return;

                if (IsDirty || IsBusy)
                {
                    OnPropertyChanged();
                    return;
                }

                SelectItem(value);
            }
        }

        public bool HasSummaries => Summaries.Count > 0;
        public bool HasNoSummaries => !HasSummaries;
        public bool HasSelectedItem => SelectedItem != null;
        public bool HasNoSelectionWithSummaries => HasSummaries && !HasSelectedItem;

        public bool IsSaving
        {
            get => _isSaving;
            private set
            {
                if (SetProperty(ref _isSaving, value))
                    NotifyStateChanged();
            }
        }

        public bool IsDeleting
        {
            get => _isDeleting;
            private set
            {
                if (SetProperty(ref _isDeleting, value))
                    NotifyStateChanged();
            }
        }

        public bool IsRegenerating
        {
            get => _isRegenerating;
            private set
            {
                if (SetProperty(ref _isRegenerating, value))
                    NotifyStateChanged();
            }
        }

        public bool IsBusy => IsSaving || IsDeleting || IsRegenerating;
        public bool IsListInteractionEnabled => !IsDirty && !IsBusy;
        public bool IsEditorEnabled => HasSelectedItem && !IsBusy;
        public bool CanRegenerateSelectedSummary =>
            SelectedItem != null &&
            _summaryService.ValidateRange(
                _request.Session,
                SelectedItem.Summary.StartMessageId,
                SelectedItem.Summary.EndMessageId,
                SelectedItem.Summary).IsSuccess;

        public bool HasRegenerateUnavailableMessage =>
            SelectedItem != null &&
            !CanRegenerateSelectedSummary;

        public string RegenerateUnavailableMessage =>
            HasRegenerateUnavailableMessage
                ? "원본 대화 범위를 찾을 수 없어 AI 재생성을 사용할 수 없습니다."
                : "";

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

        public string EditTitle
        {
            get => _editTitle;
            set
            {
                if (SetProperty(ref _editTitle, value))
                {
                    OnPropertyChanged(nameof(TitleLengthText));
                    OnPropertyChanged(nameof(IsTitleValid));
                    NotifyEditStateChanged();
                }
            }
        }

        public string EditCurrentSituation
        {
            get => _editCurrentSituation;
            set
            {
                if (SetProperty(ref _editCurrentSituation, value))
                    NotifyEditStateChanged();
            }
        }

        public string EditKeyEvents
        {
            get => _editKeyEvents;
            set
            {
                if (SetProperty(ref _editKeyEvents, value))
                    NotifyEditStateChanged();
            }
        }

        public string EditRelationshipChanges
        {
            get => _editRelationshipChanges;
            set
            {
                if (SetProperty(ref _editRelationshipChanges, value))
                    NotifyEditStateChanged();
            }
        }

        public string EditPromisesAndImportantStatements
        {
            get => _editPromisesAndImportantStatements;
            set
            {
                if (SetProperty(ref _editPromisesAndImportantStatements, value))
                    NotifyEditStateChanged();
            }
        }

        public string EditUnresolvedMatters
        {
            get => _editUnresolvedMatters;
            set
            {
                if (SetProperty(ref _editUnresolvedMatters, value))
                    NotifyEditStateChanged();
            }
        }

        public string EditPersistentState
        {
            get => _editPersistentState;
            set
            {
                if (SetProperty(ref _editPersistentState, value))
                    NotifyEditStateChanged();
            }
        }

        public string TitleLengthText => $"{EditTitle.Trim().Length} / {ConversationSummaryService.MaxTitleLength}";

        public bool IsTitleValid =>
            !string.IsNullOrWhiteSpace(EditTitle) &&
            EditTitle.Trim().Length <= ConversationSummaryService.MaxTitleLength;

        public bool IsDirty =>
            SelectedItem != null &&
            (EditTitle != SelectedItem.Summary.Title ||
             EditCurrentSituation != SelectedItem.Summary.CurrentSituation ||
             EditKeyEvents != SelectedItem.Summary.KeyEvents ||
             EditRelationshipChanges != SelectedItem.Summary.RelationshipChanges ||
             EditPromisesAndImportantStatements != SelectedItem.Summary.PromisesAndImportantStatements ||
             EditUnresolvedMatters != SelectedItem.Summary.UnresolvedMatters ||
             EditPersistentState != SelectedItem.Summary.PersistentState);

        public bool CanCloseWithoutConfirmation => !IsSaving && !IsDeleting && !IsDirty;

        public void OnWindowClosing()
        {
            _isClosingOrClosed = true;
            CancelActiveRegeneration();
        }

        public void CancelActiveRegeneration()
        {
            _regenerationCancellation?.Cancel();
        }

        private async Task SaveAsync()
        {
            if (!CanSave() || SelectedItem == null)
                return;

            var item = SelectedItem;
            IsSaving = true;
            ErrorMessage = "";

            try
            {
                var result = await _persistenceService.UpdateAsync(
                    _request.Store,
                    _request.Session,
                    item.Summary,
                    CreateDraftFromEditableFields(item),
                    CancellationToken.None);

                if (result.IsSuccess)
                {
                    item.RefreshFromSummary();
                    LoadEditableFields(item);
                    ErrorMessage = "";
                    return;
                }

                ErrorMessage = result.ErrorMessage ?? "요약 저장에 실패했습니다.";
            }
            finally
            {
                IsSaving = false;
            }
        }

        private bool CanSave() =>
            SelectedItem != null &&
            IsDirty &&
            IsTitleValid &&
            !IsBusy;

        private async Task RegenerateAsync()
        {
            if (!CanRegenerate())
                return;

            var item = SelectedItem;
            if (item == null)
                return;

            if (IsDirty && !ConfirmRegenerate())
                return;

            var cancellation = CreateRegenerationCancellation();
            IsRegenerating = true;
            ErrorMessage = "";

            try
            {
                var result = await _summarizer.RegenerateDraftAsync(
                    _request.Session,
                    _request.Character,
                    item.Summary,
                    _request.ModelId,
                    cancellation.Token);

                if (!IsCurrentActiveRegeneration(cancellation))
                    return;

                if (result.IsCanceled)
                    return;

                if (!result.IsSuccess || result.Draft == null)
                {
                    ErrorMessage = result.ErrorMessage ?? "요약 재생성에 실패했습니다.";
                    return;
                }

                ApplyDraftToEditableFields(result.Draft);
                ErrorMessage = "";
            }
            finally
            {
                bool isCurrentOperation = ReferenceEquals(_regenerationCancellation, cancellation);
                if (isCurrentOperation)
                    _regenerationCancellation = null;

                cancellation.Dispose();

                if (!_isClosingOrClosed && isCurrentOperation)
                    IsRegenerating = false;
            }
        }

        private bool CanRegenerate() =>
            !_isClosingOrClosed &&
            SelectedItem != null &&
            CanRegenerateSelectedSummary &&
            !IsBusy;

        private bool ConfirmRegenerate()
        {
            var args = new SummaryRegenerateConfirmationRequestedEventArgs();
            RegenerateConfirmationRequested?.Invoke(this, args);
            return args.Confirmed;
        }

        private async Task DeleteAsync()
        {
            if (!CanDelete() || SelectedItem == null)
                return;

            var item = SelectedItem;
            var args = new SummaryDeleteConfirmationRequestedEventArgs(IsDirty);
            DeleteConfirmationRequested?.Invoke(this, args);
            if (!args.Confirmed)
                return;

            IsDeleting = true;
            ErrorMessage = "";

            try
            {
                var result = await _persistenceService.DeleteAsync(
                    _request.Store,
                    _request.Session,
                    item.Summary,
                    CancellationToken.None);

                if (result.IsSuccess)
                {
                    Summaries.Remove(item);
                    SelectItemInternal(null, clearError: true);
                    NotifySummaryCollectionChanged();
                    ErrorMessage = "";
                    return;
                }

                ErrorMessage = result.ErrorMessage ?? "요약 삭제에 실패했습니다.";
            }
            finally
            {
                IsDeleting = false;
            }
        }

        private bool CanDelete() =>
            SelectedItem != null &&
            !IsBusy;

        private void DiscardChanges()
        {
            if (!CanDiscardChanges() || SelectedItem == null)
                return;

            ErrorMessage = "";
            LoadEditableFields(SelectedItem);
        }

        private bool CanDiscardChanges() =>
            SelectedItem != null &&
            IsDirty &&
            !IsBusy;

        private void SelectItem(SummaryManagementItemViewModel? item)
        {
            SelectItemInternal(item, clearError: true);
        }

        private void SelectItemInternal(SummaryManagementItemViewModel? item, bool clearError)
        {
            if (SetProperty(ref _selectedItem, item, nameof(SelectedItem)))
            {
                if (clearError)
                    ErrorMessage = "";
                if (item != null)
                    LoadEditableFields(item);
                else
                    ClearEditableFields();

                OnPropertyChanged(nameof(HasSelectedItem));
                OnPropertyChanged(nameof(HasNoSelectionWithSummaries));
                OnPropertyChanged(nameof(IsEditorEnabled));
                OnPropertyChanged(nameof(CanRegenerateSelectedSummary));
                OnPropertyChanged(nameof(HasRegenerateUnavailableMessage));
                OnPropertyChanged(nameof(RegenerateUnavailableMessage));
                NotifyEditStateChanged();
            }
        }

        private void LoadEditableFields(SummaryManagementItemViewModel item)
        {
            EditTitle = item.Summary.Title;
            EditCurrentSituation = item.Summary.CurrentSituation;
            EditKeyEvents = item.Summary.KeyEvents;
            EditRelationshipChanges = item.Summary.RelationshipChanges;
            EditPromisesAndImportantStatements = item.Summary.PromisesAndImportantStatements;
            EditUnresolvedMatters = item.Summary.UnresolvedMatters;
            EditPersistentState = item.Summary.PersistentState;
            NotifyEditStateChanged();
        }

        private void ClearEditableFields()
        {
            EditTitle = "";
            EditCurrentSituation = "";
            EditKeyEvents = "";
            EditRelationshipChanges = "";
            EditPromisesAndImportantStatements = "";
            EditUnresolvedMatters = "";
            EditPersistentState = "";
            NotifyEditStateChanged();
        }

        private SummaryDraft CreateDraftFromEditableFields(SummaryManagementItemViewModel item) =>
            new()
            {
                StartMessageId = item.Summary.StartMessageId,
                EndMessageId = item.Summary.EndMessageId,
                Title = EditTitle,
                CurrentSituation = EditCurrentSituation,
                KeyEvents = EditKeyEvents,
                RelationshipChanges = EditRelationshipChanges,
                PromisesAndImportantStatements = EditPromisesAndImportantStatements,
                UnresolvedMatters = EditUnresolvedMatters,
                PersistentState = EditPersistentState
            };

        private void ApplyDraftToEditableFields(SummaryDraft draft)
        {
            EditTitle = draft.Title;
            EditCurrentSituation = draft.CurrentSituation;
            EditKeyEvents = draft.KeyEvents;
            EditRelationshipChanges = draft.RelationshipChanges;
            EditPromisesAndImportantStatements = draft.PromisesAndImportantStatements;
            EditUnresolvedMatters = draft.UnresolvedMatters;
            EditPersistentState = draft.PersistentState;
            NotifyEditStateChanged();
        }

        private CancellationTokenSource CreateRegenerationCancellation()
        {
            _regenerationCancellation?.Cancel();
            _regenerationCancellation?.Dispose();
            _regenerationCancellation = new CancellationTokenSource();
            return _regenerationCancellation;
        }

        private bool IsCurrentActiveRegeneration(CancellationTokenSource cancellation)
        {
            return !_isClosingOrClosed &&
                   !cancellation.IsCancellationRequested &&
                   ReferenceEquals(_regenerationCancellation, cancellation);
        }

        private void NotifySummaryCollectionChanged()
        {
            OnPropertyChanged(nameof(HasSummaries));
            OnPropertyChanged(nameof(HasNoSummaries));
            OnPropertyChanged(nameof(HasNoSelectionWithSummaries));
        }

        private void NotifyEditStateChanged()
        {
            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(CanCloseWithoutConfirmation));
            OnPropertyChanged(nameof(IsListInteractionEnabled));
            SaveCommand.NotifyCanExecuteChanged();
            RegenerateCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            DiscardChangesCommand.NotifyCanExecuteChanged();
        }

        private void NotifyStateChanged()
        {
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(CanCloseWithoutConfirmation));
            OnPropertyChanged(nameof(IsListInteractionEnabled));
            OnPropertyChanged(nameof(IsEditorEnabled));
            SaveCommand.NotifyCanExecuteChanged();
            RegenerateCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            DiscardChangesCommand.NotifyCanExecuteChanged();
        }
    }
}
