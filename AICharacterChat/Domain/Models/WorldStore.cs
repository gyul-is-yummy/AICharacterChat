using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace AICharacterChat.Domain.Models
{
    public class WorldStore
    {
        public List<World> Worlds { get; set; } = new();
        public string ActiveWorldId { get; set; } = "";

        [JsonIgnore]
        public World? ActiveWorld =>
            Worlds.FirstOrDefault(w => w.Id == ActiveWorldId);
    }
}
