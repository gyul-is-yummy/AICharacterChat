using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AICharacterChat.Domain.Models;
using DomainCustomField = AICharacterChat.Domain.Models.CustomField;

namespace AICharacterChat
{
    public partial class CharacterSettingsWindow : Window
    {
        private readonly Character _current;
        private readonly World _world;
        private readonly Func<Task> _saveAsync;
        private readonly Dictionary<string, TextBox> _relationBoxes = new();

        public Character ResultProfile { get; private set; } = new();
        public string SelectedUserPersonaId { get; private set; } = "";

        public CharacterSettingsWindow(
            Character current,
            List<Character> otherCharacters,
            World world,
            Func<Task> saveAsync)
        {
            InitializeComponent();
            _current = current;
            _world = world;
            _saveAsync = saveAsync;

            NameBox.Text = current.Name;
            AgeBox.Text = current.Age;
            GenderBox.SelectedIndex = current.Gender switch
            {
                "남" => 0,
                "여" => 1,
                _ => 2
            };
            JobBox.Text = current.Job;
            AppearanceBox.Text = current.Appearance;
            PersonalityBox.Text = current.Personality;
            EtcBox.Text = current.Etc;
            SecretBox.Text = current.Secret;
            SpeechStyleBox.Text = current.SpeechStyle;
            SituationBox.Text = current.DefaultScenario;

            BuildRelationshipUI(current, otherCharacters);

            foreach (var field in current.CustomFields)
                AddCustomFieldRow(field.Label, field.Value);

            string selectedUserPersonaId =
                world.GetSessionForCharacter(current.Id)?.UserPersonaId
                ?? world.UserPersonas.FirstOrDefault()?.Id
                ?? "";
            RefreshUserProfileComboBox(selectedUserPersonaId);
        }

        private void AddFieldButton_Click(object sender, RoutedEventArgs e)
            => AddCustomFieldRow("", "");

        private void AddCustomFieldRow(string label, string value)
        {
            var container = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(16, 14, 36)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 50, 100)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(8)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var labelBox = new TextBox
            {
                Text = label,
                Background = new SolidColorBrush(Color.FromRgb(22, 18, 50)),
                Foreground = new SolidColorBrush(Color.FromRgb(180, 160, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 60, 120)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center,
                Tag = "label"
            };

            var valueBox = new TextBox
            {
                Text = value,
                Background = new SolidColorBrush(Color.FromRgb(22, 18, 50)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 60, 120)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 12,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MinHeight = 32,
                MaxHeight = 80,
                Tag = "value"
            };

            var deleteBtn = new Button
            {
                Content = "✕",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 80, 80)),
                BorderThickness = new Thickness(0),
                FontSize = 13,
                Padding = new Thickness(4, 0, 0, 0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Top
            };
            deleteBtn.Click += (_, _) => CustomFieldsPanel.Children.Remove(container);

            Grid.SetColumn(labelBox, 0);
            Grid.SetColumn(valueBox, 2);
            Grid.SetColumn(deleteBtn, 4);

            grid.Children.Add(labelBox);
            grid.Children.Add(valueBox);
            grid.Children.Add(deleteBtn);
            container.Child = grid;
            CustomFieldsPanel.Children.Add(container);
        }

        private List<DomainCustomField> CollectCustomFields()
        {
            var result = new List<DomainCustomField>();
            foreach (Border container in CustomFieldsPanel.Children)
            {
                var grid = (Grid)container.Child;
                var labelBox = grid.Children.OfType<TextBox>()
                    .FirstOrDefault(t => (string)t.Tag == "label");
                var valueBox = grid.Children.OfType<TextBox>()
                    .FirstOrDefault(t => (string)t.Tag == "value");

                if (labelBox == null || valueBox == null)
                    continue;
                if (string.IsNullOrWhiteSpace(labelBox.Text) &&
                    string.IsNullOrWhiteSpace(valueBox.Text))
                    continue;

                result.Add(new DomainCustomField
                {
                    Label = labelBox.Text.Trim(),
                    Value = valueBox.Text.Trim()
                });
            }

            return result;
        }

        private void RefreshUserProfileComboBox(string selectedId)
        {
            UserProfileComboBox.Items.Clear();
            foreach (var up in _world.UserPersonas)
            {
                UserProfileComboBox.Items.Add(new ComboBoxItem
                {
                    Content = up.Name,
                    Tag = up.Id
                });
            }

            var target = UserProfileComboBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(i => (string)i.Tag == selectedId);
            UserProfileComboBox.SelectedItem = target
                ?? UserProfileComboBox.Items.OfType<ComboBoxItem>().FirstOrDefault();
        }

        private void ManageUserProfiles_Click(object sender, RoutedEventArgs e)
        {
            string currentId = (UserProfileComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            var win = new UserProfileManagerWindow(_world, _saveAsync) { Owner = this };
            win.ShowDialog();
            RefreshUserProfileComboBox(currentId);
        }

        private void BuildRelationshipUI(Character current, List<Character> others)
        {
            RelationshipsPanel.Children.Clear();
            _relationBoxes.Clear();

            if (others.Count == 0)
            {
                RelationshipsPanel.Children.Add(new TextBlock
                {
                    Text = "세계관에 다른 캐릭터가 없습니다.",
                    Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 150)),
                    FontSize = 12,
                    Margin = new Thickness(0, 4, 0, 4)
                });
                return;
            }

            foreach (var other in others)
            {
                var existing = current.Relationships
                    .FirstOrDefault(r => r.TargetCharacterId == other.Id);

                RelationshipsPanel.Children.Add(new TextBlock
                {
                    Text = other.Name,
                    Foreground = new SolidColorBrush(Color.FromRgb(200, 180, 255)),
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 6, 0, 3)
                });

                var box = new TextBox
                {
                    Text = existing?.Description ?? "",
                    Background = new SolidColorBrush(Color.FromRgb(18, 15, 40)),
                    Foreground = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(80, 60, 120)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(6),
                    FontSize = 12,
                    Height = 54,
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                };
                RelationshipsPanel.Children.Add(box);
                _relationBoxes[other.Id] = box;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                MessageBox.Show("캐릭터 이름을 입력해주세요.", "알림");
                return;
            }

            string selectedUserProfileId =
                (UserProfileComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
            SelectedUserPersonaId = selectedUserProfileId;

            var relationships = _relationBoxes
                .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value.Text))
                .Select(kvp => new CharacterRelationship
                {
                    TargetCharacterId = kvp.Key,
                    Description = kvp.Value.Text.Trim()
                })
                .ToList();

            ResultProfile = new Character
            {
                Id = _current.Id,
                Name = NameBox.Text.Trim(),
                Age = AgeBox.Text.Trim(),
                Gender = (GenderBox.SelectedItem as ComboBoxItem)?.Content as string ?? "기타",
                Job = JobBox.Text.Trim(),
                Appearance = AppearanceBox.Text.Trim(),
                Personality = PersonalityBox.Text.Trim(),
                Relationships = relationships,
                Etc = EtcBox.Text.Trim(),
                Secret = SecretBox.Text.Trim(),
                SpeechStyle = SpeechStyleBox.Text.Trim(),
                DefaultScenario = SituationBox.Text.Trim(),
                CustomFields = CollectCustomFields(),
                Lore = _current.Lore
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
