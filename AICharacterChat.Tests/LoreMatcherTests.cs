using System.Collections.Generic;
using AICharacterChat.Application.Chat;
using AICharacterChat.Domain.Models;
using Xunit;

namespace AICharacterChat.Tests
{
    public class LoreMatcherTests
    {
        [Fact]
        public void MatchesEnabledKeywordsCaseInsensitively()
        {
            var entries = new List<LoreEntry>
            {
                new() { Title = "Moon", Keywords = ["Moon"], Content = "달", IsEnabled = true },
                new() { Title = "Disabled", Keywords = ["sun"], Content = "해", IsEnabled = false }
            };

            var matches = new LoreMatcher().Match(entries, "the moon is bright");

            Assert.Single(matches);
            Assert.Equal("Moon", matches[0].Title);
        }

        [Fact]
        public void IgnoresEmptyInputAndBlankKeywords()
        {
            var entries = new List<LoreEntry>
            {
                new() { Title = "Blank", Keywords = [" ", ""], IsEnabled = true }
            };

            Assert.Empty(new LoreMatcher().Match(entries, ""));
            Assert.Empty(new LoreMatcher().Match(entries, "anything"));
        }
    }
}
