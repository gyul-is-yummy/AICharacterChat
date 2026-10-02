using System;

namespace AICharacterChat.Presentation.Models
{
    public sealed class SummaryDeleteConfirmationRequestedEventArgs : EventArgs
    {
        public SummaryDeleteConfirmationRequestedEventArgs(bool hasUnsavedChanges)
        {
            HasUnsavedChanges = hasUnsavedChanges;
        }

        public bool HasUnsavedChanges { get; }
        public bool Confirmed { get; set; }
    }
}
