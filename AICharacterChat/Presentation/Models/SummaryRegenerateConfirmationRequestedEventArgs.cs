using System;

namespace AICharacterChat.Presentation.Models
{
    public sealed class SummaryRegenerateConfirmationRequestedEventArgs : EventArgs
    {
        public bool Confirmed { get; set; }
    }
}
