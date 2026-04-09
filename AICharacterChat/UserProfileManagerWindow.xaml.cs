using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AICharacterChat
{
    public partial class UserProfileManagerWindow : Window
    {
        private readonly WorldProfile _world;
        private readonly WorldManager _manager;

        public UserProfileManagerWindow(WorldProfile world, WorldManager manager)
        {
            InitializeComponent();
            _world = world;
            _manager = manager;
            RefreshList();
        }

        private void RefreshList()
        {
            ProfileListPanel.Children.Clear();
            foreach (var profile in _world.UserProfiles)
                ProfileListPanel.Children.Add(CreateItem(profile));
        }

        private Border CreateItem(UserProfile profile)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(22, 18, 50)),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 0, 4),
                Padding = new Thickness(8, 6, 4, 6)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameText = new TextBlock
            {
                Text = profile.Name,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 150)),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(nameText, 0);

            var editBtn = new Button
            {
                Content = "✏",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 150, 100)),
                BorderThickness = new Thickness(0),
                FontSize = 13,
                Padding = new Thickness(6, 0, 4, 0),
                Cursor = Cursors.Hand,
                Tag = profile.Id,
                ToolTip = "편집"
            };
            editBtn.Click += EditProfile_Click;
            Grid.SetColumn(editBtn, 1);

            var deleteBtn = new Button
            {
                Content = "🗑",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 150, 100)),
                BorderThickness = new Thickness(0),
                FontSize = 12,
                Padding = new Thickness(4, 0, 0, 0),
                Cursor = Cursors.Hand,
                Tag = profile.Id,
                ToolTip = "삭제"
            };
            deleteBtn.Click += DeleteProfile_Click;
            Grid.SetColumn(deleteBtn, 2);

            grid.Children.Add(nameText);
            grid.Children.Add(editBtn);
            grid.Children.Add(deleteBtn);
            border.Child = grid;
            return border;
        }

        private void AddProfile_Click(object sender, RoutedEventArgs e)
        {
            var win = new UserProfileSettingsWindow(new UserProfile());
            win.Owner = this;
            if (win.ShowDialog() != true) return;

            _world.UserProfiles.Add(win.ResultProfile);
            _manager.Save();
            RefreshList();
        }

        private void EditProfile_Click(object sender, RoutedEventArgs e)
        {
            string id = (string)((Button)sender).Tag;
            var profile = _world.UserProfiles.FirstOrDefault(u => u.Id == id);
            if (profile == null) return;

            var win = new UserProfileSettingsWindow(profile);
            win.Owner = this;
            if (win.ShowDialog() != true) return;

            var r = win.ResultProfile;
            profile.Name = r.Name;
            profile.Appearance = r.Appearance;
            profile.Personality = r.Personality;
            profile.AdditionalInfo = r.AdditionalInfo;
            _manager.Save();
            RefreshList();
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_world.UserProfiles.Count <= 1)
            {
                MessageBox.Show("유저 프로필이 한 개 이상 있어야 합니다.", "알림");
                return;
            }

            string id = (string)((Button)sender).Tag;
            var profile = _world.UserProfiles.FirstOrDefault(u => u.Id == id);
            if (profile == null) return;

            var confirm = MessageBox.Show(
                $"'{profile.Name}' 프로필을 삭제할까요?",
                "삭제 확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            _world.UserProfiles.RemoveAll(u => u.Id == id);
            _manager.Save();
            RefreshList();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
            => Close();
    }
}