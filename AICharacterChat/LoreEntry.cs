using System;
using System.Collections.Generic;

namespace AICharacterChat
{
    public class LoreEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "";
        public List<string> Keywords { get; set; } = new();
        public string Content { get; set; } = "";
        public bool IsEnabled { get; set; } = true;
    }
}
