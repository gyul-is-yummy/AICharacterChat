using System;

namespace AICharacterChat.Application.Models
{
    public class AnthropicApiKeyReadException : Exception
    {
        public AnthropicApiKeyReadException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
}
