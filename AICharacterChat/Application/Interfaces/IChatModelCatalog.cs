using System.Collections.Generic;
using AICharacterChat.Application.Models;

namespace AICharacterChat.Application.Interfaces
{
    public interface IChatModelCatalog
    {
        IReadOnlyList<ChatModelOption> Models { get; }
    }
}
