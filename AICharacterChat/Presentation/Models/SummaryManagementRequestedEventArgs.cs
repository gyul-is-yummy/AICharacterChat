using System;

namespace AICharacterChat.Presentation.Models
{
    public sealed class SummaryManagementRequestedEventArgs : EventArgs
    {
        public SummaryManagementRequestedEventArgs(SummaryManagementRequest request)
        {
            Request = request;
        }

        public SummaryManagementRequest Request { get; }
    }
}
