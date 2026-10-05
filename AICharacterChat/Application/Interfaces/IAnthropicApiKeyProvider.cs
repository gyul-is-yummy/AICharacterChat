using System.Threading;
using System.Threading.Tasks;

namespace AICharacterChat.Application.Interfaces
{
    public interface IAnthropicApiKeyProvider
    {
        Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default);
    }
}
