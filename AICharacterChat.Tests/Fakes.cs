using System;
using System.Threading;
using System.Threading.Tasks;
using AICharacterChat.Application.Interfaces;
using AICharacterChat.Application.Models;
using AICharacterChat.Domain.Models;

namespace AICharacterChat.Tests
{
    internal class FakeChatModelClient : IChatModelClient
    {
        public string Reply { get; init; } = "";
        public Exception? Exception { get; init; }
        public bool Cancel { get; init; }
        public ChatCompletionRequest? LastRequest { get; private set; }

        public Task<string> SendAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            if (Cancel)
                throw new OperationCanceledException(cancellationToken);
            if (Exception != null)
                throw Exception;

            return Task.FromResult(Reply);
        }
    }

    internal class FakeWorldRepository : IWorldRepository
    {
        public int SaveCount { get; private set; }
        public Exception? SaveException { get; init; }
        public bool CancelSave { get; init; }

        public Task<WorldStore> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorldStore());

        public Task SaveAsync(WorldStore store, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            if (CancelSave)
                throw new OperationCanceledException(cancellationToken);
            if (SaveException != null)
                throw SaveException;

            return Task.CompletedTask;
        }
    }
}
