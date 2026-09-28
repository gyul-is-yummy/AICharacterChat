using System;
using System.Collections.Generic;
using System.Linq;
using AICharacterChat.Domain.Enums;
using AICharacterChat.Domain.Models;

namespace AICharacterChat.Application.Context
{
    public class RecentMessageSelector
    {
        public const int DefaultMaxRecentMessages = 20;

        public RecentMessageSelector(int maxRecentMessages = DefaultMaxRecentMessages)
        {
            if (maxRecentMessages <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxRecentMessages));

            MaxRecentMessages = maxRecentMessages;
        }

        public int MaxRecentMessages { get; }

        public IReadOnlyList<ChatMessage> Select(IReadOnlyList<ChatMessage> messages)
        {
            ArgumentNullException.ThrowIfNull(messages);

            if (messages.Count <= MaxRecentMessages)
                return messages.ToList();

            var selected = messages
                .Skip(messages.Count - MaxRecentMessages)
                .ToList();

            if (!selected.Any(message => message.Role == ChatRole.User))
                return selected;

            while (selected.Count > 0 && selected[0].Role == ChatRole.Assistant)
                selected.RemoveAt(0);

            return selected;
        }
    }
}
