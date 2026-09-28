using System;
using System.Collections.Generic;
using System.Linq;
using AICharacterChat.Domain.Models;

namespace AICharacterChat.Application.Chat
{
    public class LoreMatcher
    {
        public List<AICharacterChat.Domain.Models.LoreEntry> Match(
            IEnumerable<AICharacterChat.Domain.Models.LoreEntry> entries,
            string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return [];

            return entries
                .Where(entry => entry.IsEnabled &&
                                entry.Keywords.Any(keyword =>
                                    !string.IsNullOrWhiteSpace(keyword) &&
                                    input.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }
    }
}
