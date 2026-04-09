using System.Windows;

namespace AICharacterChat
{
    public partial class WorldSettingsWindow : Window
    {
        public WorldProfile ResultWorld { get; private set; }

        public WorldSettingsWindow(WorldProfile current)
        {
            InitializeComponent();
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
            ResultWorld = new WorldProfile
            {
                Name = NameBox.Text.Trim(),
                Genre = GenreBox.Text.Trim(),
                Era = EraBox.Text.Trim(),
                Description = DescriptionBox.Text.Trim(),
                Rules = RulesBox.Text.Trim()
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