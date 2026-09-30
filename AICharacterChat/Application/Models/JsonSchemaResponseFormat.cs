using System.Collections.Generic;

namespace AICharacterChat.Application.Models
{
    public sealed class JsonSchemaResponseFormat
    {
        public IReadOnlyDictionary<string, object> Schema { get; init; } =
            new Dictionary<string, object>();
    }
}
