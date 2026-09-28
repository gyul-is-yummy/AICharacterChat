using System.Threading;
using System.Threading.Tasks;
using AICharacterChat.Application.Models;

namespace AICharacterChat.Application.Interfaces
{
    public interface IChatModelClient
    {
        Task<string> SendAsync(
            ChatCompletionRequest request,
            CancellationToken cancellationToken = default);
    }
}
