using System.Threading;
using System.Threading.Tasks;
using AICharacterChat.Application.Models;

namespace AICharacterChat.Application.Interfaces
{
    public interface IAnthropicApiKeyStore : IAnthropicApiKeyProvider
    {
        Task<AnthropicApiKeyStatus> GetStatusAsync(CancellationToken cancellationToken = default);
        Task SaveAsync(string apiKey, CancellationToken cancellationToken = default);
        Task DeleteAsync(CancellationToken cancellationToken = default);
    }
}
