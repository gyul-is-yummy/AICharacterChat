namespace AICharacterChat.Application.Models
{
    public class ChatModelOption
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";

        public override string ToString() => Label;
    }
}
