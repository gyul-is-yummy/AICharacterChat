using System.Collections.ObjectModel;
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
        private readonly SummaryManagementRequest _request;
        private SummaryManagementItemViewModel? _selectedItem;
        private bool _isSaving;
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
            SummaryManagementRequest request)
        {
            _persistenceService = persistenceService;
            _request = request;

            SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
            DiscardChangesCommand = new RelayCommand(DiscardChanges, CanDiscardChanges);

            foreach (var summary in request.Session.Summaries)
                Summaries.Add(new SummaryManagementItemViewModel(summary));

            if (Summaries.Count > 0)
                SelectItem(Summaries[0]);
        }

        public ObservableCollection<SummaryManagementItemViewModel> Summaries { get; } = new();

        public IAsyncRelayCommand SaveCommand { get; }
        public IRelayCommand DiscardChangesCommand { get; }

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

        public bool IsSaving
        {
            get => _isSaving;
            private set
            {
                if (SetProperty(ref _isSaving, value))
                    NotifyStateChanged();
            }
        }

        public bool IsBusy => IsSaving;
        public bool IsListInteractionEnabled => !IsDirty && !IsBusy;
        public bool IsEditorEnabled => HasSelectedItem && !IsBusy;

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

        public bool CanCloseWithoutConfirmation => !IsSaving && !IsDirty;

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
            if (SetProperty(ref _selectedItem, item, nameof(SelectedItem)))
            {
                ErrorMessage = "";
                if (item != null)
                    LoadEditableFields(item);
                else
                    ClearEditableFields();

                OnPropertyChanged(nameof(HasSelectedItem));
                OnPropertyChanged(nameof(IsEditorEnabled));
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

        private void NotifyEditStateChanged()
        {
            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(CanCloseWithoutConfirmation));
            OnPropertyChanged(nameof(IsListInteractionEnabled));
            SaveCommand.NotifyCanExecuteChanged();
            DiscardChangesCommand.NotifyCanExecuteChanged();
        }

        private void NotifyStateChanged()
        {
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(CanCloseWithoutConfirmation));
            OnPropertyChanged(nameof(IsListInteractionEnabled));
            OnPropertyChanged(nameof(IsEditorEnabled));
            SaveCommand.NotifyCanExecuteChanged();
            DiscardChangesCommand.NotifyCanExecuteChanged();
        }
    }
}
