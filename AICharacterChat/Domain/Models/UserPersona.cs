using System;

namespace AICharacterChat.Domain.Models
{
    public class UserPersona
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "나";
        public string Appearance { get; set; } = "";
        public string Personality { get; set; } = "";
        public string AdditionalInfo { get; set; } = "";
    }
}
