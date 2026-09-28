using System.Windows;
using AICharacterChat.Domain.Models;

namespace AICharacterChat
{
    public partial class WorldSettingsWindow : Window
    {
        private readonly World _current;

        public World ResultWorld { get; private set; } = new();

        public WorldSettingsWindow(World current)
        {
            InitializeComponent();
            _current = current;
            NameBox.Text = current.Name;
            GenreBox.Text = current.Genre;
            EraBox.Text = current.Era;
            DescriptionBox.Text = current.Description;
            RulesBox.Text = current.Rules;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                MessageBox.Show("세계관 이름을 입력해주세요.", "알림");
                return;
            }

            ResultWorld = new World
            {
                Id = _current.Id,
                Name = NameBox.Text.Trim(),
                Genre = GenreBox.Text.Trim(),
                Era = EraBox.Text.Trim(),
                Description = DescriptionBox.Text.Trim(),
                Rules = RulesBox.Text.Trim(),
                ActiveCharacterId = _current.ActiveCharacterId,
                Characters = _current.Characters,
                UserPersonas = _current.UserPersonas,
                ChatSessions = _current.ChatSessions
            };
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
