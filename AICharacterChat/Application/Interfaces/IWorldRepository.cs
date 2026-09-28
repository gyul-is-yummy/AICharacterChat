using System.Threading;
using System.Threading.Tasks;
using AICharacterChat.Domain.Models;

namespace AICharacterChat.Application.Interfaces
{
    public interface IWorldRepository
    {
        Task<WorldStore> LoadAsync(CancellationToken cancellationToken = default);
        Task SaveAsync(WorldStore store, CancellationToken cancellationToken = default);
    }
}
