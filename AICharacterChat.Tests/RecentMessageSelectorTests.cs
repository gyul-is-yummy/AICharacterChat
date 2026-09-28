using System;
using System.Linq;
using AICharacterChat.Application.Context;
using AICharacterChat.Domain.Enums;
using AICharacterChat.Domain.Models;
using Xunit;

namespace AICharacterChat.Tests
{
    public class RecentMessageSelectorTests
    {
        [Fact]
        public void UnderLimitReturnsAllMessages()
        {
            var messages = CreateMessages(ChatRole.User, ChatRole.Assistant, ChatRole.User);

            var selected = new RecentMessageSelector(5).Select(messages);

            Assert.Equal(messages, selected);
        }

        [Fact]
        public void OverLimitReturnsMostRecentMessages()
        {
            var messages = Enumerable.Range(1, 30)
                .Select(i => new ChatMessage(i % 2 == 0 ? ChatRole.Assistant : ChatRole.User, i.ToString()))
                .ToList();

            var selected = new RecentMessageSelector(20).Select(messages);

            Assert.Equal(20, selected.Count);
            Assert.Equal("11", selected[0].Content);
            Assert.Equal("30", selected[^1].Content);
        }

        [Fact]
        public void PreservesChronologicalOrder()
        {
            var messages = Enumerable.Range(1, 8)
                .Select(i => new ChatMessage(ChatRole.User, i.ToString()))
                .ToList();

            var selected = new RecentMessageSelector(3).Select(messages);

            Assert.Equal(["6", "7", "8"], selected.Select(m => m.Content));
        }

        [Fact]
        public void DoesNotMutateSourceMessages()
        {
            var messages = CreateMessages(
                ChatRole.User,
                ChatRole.Assistant,
                ChatRole.User,
                ChatRole.Assistant);
            var snapshot = messages
                .Select(m => new { m.Role, m.Content })
                .ToList();

            _ = new RecentMessageSelector(2).Select(messages);

            Assert.Equal(4, messages.Count);
            Assert.Equal(snapshot.Select(s => s.Role), messages.Select(m => m.Role));
            Assert.Equal(snapshot.Select(s => s.Content), messages.Select(m => m.Content));
        }

        [Fact]
        public void IncludesLatestUserMessage()
        {
            var messages = Enumerable.Range(1, 25)
                .Select(i => new ChatMessage(ChatRole.Assistant, i.ToString()))
                .ToList();
            messages.Add(new ChatMessage(ChatRole.User, "latest user"));

            var selected = new RecentMessageSelector(20).Select(messages);

            Assert.Contains(selected, message =>
                message.Role == ChatRole.User &&
                message.Content == "latest user");
        }

        [Fact]
        public void RemovesLeadingAssistantsAfterTruncation()
        {
            var messages = CreateMessages(
                ChatRole.User,
                ChatRole.Assistant,
                ChatRole.Assistant,
                ChatRole.Assistant,
                ChatRole.User,
                ChatRole.Assistant);

            var selected = new RecentMessageSelector(4).Select(messages);

            Assert.Equal(ChatRole.User, selected[0].Role);
            Assert.Equal(["5", "6"], selected.Select(m => m.Content));
        }

        [Fact]
        public void KeepsWindowWhenNoUserExistsInTruncatedWindow()
        {
            var messages = CreateMessages(
                ChatRole.User,
                ChatRole.Assistant,
                ChatRole.Assistant,
                ChatRole.Assistant);

            var selected = new RecentMessageSelector(2).Select(messages);

            Assert.Equal(2, selected.Count);
            Assert.All(selected, message => Assert.Equal(ChatRole.Assistant, message.Role));
        }

        [Fact]
        public void RejectsZeroMaxMessageCount()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RecentMessageSelector(0));
        }

        [Fact]
        public void RejectsNegativeMaxMessageCount()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RecentMessageSelector(-1));
        }

        private static List<ChatMessage> CreateMessages(params ChatRole[] roles)
        {
            return roles
                .Select((role, index) => new ChatMessage(role, (index + 1).ToString()))
                .ToList();
        }
    }
}
