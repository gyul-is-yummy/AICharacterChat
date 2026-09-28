using System.Threading;
using System.Threading.Tasks;
using AICharacterChat.Domain.Models;

namespace AICharacterChat.Application.Interfaces
{
    public interface ISettingsRepository
    {
        Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
        Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
    }
}
