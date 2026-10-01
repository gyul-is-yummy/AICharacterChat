using System;

namespace AICharacterChat.Presentation.Models
{
    public sealed class SummaryPreviewCloseRequestedEventArgs : EventArgs
    {
        public SummaryPreviewCloseRequestedEventArgs(SummaryPreviewResult result)
        {
            Result = result;
        }

        public SummaryPreviewResult Result { get; }
    }
}
