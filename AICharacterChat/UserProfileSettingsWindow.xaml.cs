using System.Windows;
using AICharacterChat.Domain.Models;

namespace AICharacterChat
{
    public partial class UserProfileSettingsWindow : Window
    {
        private readonly UserPersona _current;

        public UserPersona ResultProfile { get; private set; } = new();

        public UserProfileSettingsWindow(UserPersona current)
        {
            InitializeComponent();
            _current = current;
            NameBox.Text = current.Name;
            AppearanceBox.Text = current.Appearance;
            PersonalityBox.Text = current.Personality;
            AdditionalInfoBox.Text = current.AdditionalInfo;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                MessageBox.Show("이름을 입력해주세요.", "알림");
                return;
            }

            ResultProfile = new UserPersona
            {
                Id = _current.Id,
                Name = NameBox.Text.Trim(),
                Appearance = AppearanceBox.Text.Trim(),
                Personality = PersonalityBox.Text.Trim(),
                AdditionalInfo = AdditionalInfoBox.Text.Trim()
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
