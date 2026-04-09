using System.Windows;

namespace AICharacterChat
{
    public partial class UserProfileSettingsWindow : Window
    {
        public UserProfile ResultProfile { get; private set; }

        public UserProfileSettingsWindow(UserProfile current)
        {
            InitializeComponent();
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
            ResultProfile = new UserProfile
            {
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