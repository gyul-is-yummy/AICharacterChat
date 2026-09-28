using System;

namespace AICharacterChat.Infrastructure.AI.Anthropic
{
    public class ChatModelException : Exception
    {
        public int? StatusCode { get; }

        public ChatModelException(string message, int? statusCode = null, Exception? innerException = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
        }
    }
}
