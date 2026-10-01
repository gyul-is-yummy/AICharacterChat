using System;

namespace AICharacterChat.Presentation.Models
{
    public sealed class SummaryPreviewRequestedEventArgs : EventArgs
    {
        public SummaryPreviewRequestedEventArgs(SummaryPreviewRequest request)
        {
            Request = request;
        }

        public SummaryPreviewRequest Request { get; }
        public SummaryPreviewResult Result { get; set; } = SummaryPreviewResult.Canceled;
    }
}
