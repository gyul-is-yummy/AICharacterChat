using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AICharacterChat.Domain.Models;
using AICharacterChat.Presentation.ViewModels;

namespace AICharacterChat
{
    public partial class LoreBookWindow : Window
    {
        private readonly Character _character;
        private readonly LoreBookViewModel _viewModel;
        private LoreEntry? _editingEntry;

        public LoreBookWindow(Character character, Func<Task> saveAsync)
        {
            InitializeComponent();
            _character = character;
            _viewModel = new LoreBookViewModel(character, saveAsync);
            Title = $"로어북 - {character.Name}";
            RefreshList();
        }

        private void RefreshList()
        {
            LoreListPanel.Children.Clear();

            if (_viewModel.Entries.Count == 0)
            {
                LoreListPanel.Children.Add(new TextBlock
                {
                    Text = "등록된 로어 항목이 없습니다.\n아래 버튼으로 항목을 추가해보세요.",
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 140)),
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(4, 12, 4, 4)
                });
                return;
            }

            foreach (var entry in _viewModel.Entries)
                LoreListPanel.Children.Add(CreateEntryRow(entry));
        }

        private Border CreateEntryRow(LoreEntry entry)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(18, 18, 46)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 45, 90)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(10, 8, 8, 8)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var checkbox = new CheckBox
            {
                IsChecked = entry.IsEnabled,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                Tag = entry.Id
            };
            checkbox.Checked += async (_, _) => await ToggleEntryAsync(entry, true);
            checkbox.Unchecked += async (_, _) => await ToggleEntryAsync(entry, false);
            Grid.SetColumn(checkbox, 0);

            var infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            infoPanel.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(entry.Title) ? "(제목 없음)" : entry.Title,
                Foreground = entry.IsEnabled
                    ? new SolidColorBrush(Color.FromRgb(220, 190, 255))
                    : new SolidColorBrush(Color.FromRgb(110, 100, 140)),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold
            });
            string keywordsPreview = entry.Keywords.Count > 0
                ? string.Join(", ", entry.Keywords.Take(5)) + (entry.Keywords.Count > 5 ? " ..." : "")
                : "(키워드 없음)";
            infoPanel.Children.Add(new TextBlock
            {
                Text = keywordsPreview,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 120, 170)),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(infoPanel, 1);

            var editBtn = new Button
            {
                Content = "✎",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 130, 200)),
                BorderThickness = new Thickness(0),
                FontSize = 15,
                Padding = new Thickness(6, 0, 4, 0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = entry.Id,
                ToolTip = "편집"
            };
            editBtn.Click += EditEntry_Click;
            Grid.SetColumn(editBtn, 2);

            var deleteBtn = new Button
            {
                Content = "🗑",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 80, 80)),
                BorderThickness = new Thickness(0),
                FontSize = 13,
                Padding = new Thickness(4, 0, 0, 0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = entry.Id,
                ToolTip = "삭제"
            };
            deleteBtn.Click += DeleteEntry_Click;
            Grid.SetColumn(deleteBtn, 3);

            grid.Children.Add(checkbox);
            grid.Children.Add(infoPanel);
            grid.Children.Add(editBtn);
            grid.Children.Add(deleteBtn);
            border.Child = grid;
            return border;
        }

        private async Task ToggleEntryAsync(LoreEntry entry, bool isEnabled)
        {
            entry.IsEnabled = isEnabled;
            await _viewModel.ToggleAsync(entry, isEnabled);
            RefreshList();
        }

        private void AddEntryButton_Click(object sender, RoutedEventArgs e)
        {
            _editingEntry = null;
            EditPanelTitle.Text = "항목 추가";
            TitleBox.Text = "";
            KeywordsBox.Text = "";
            ContentBox.Text = "";
            EditPanel.Visibility = Visibility.Visible;
            TitleBox.Focus();
        }

        private void EditEntry_Click(object sender, RoutedEventArgs e)
        {
            string id = (string)((Button)sender).Tag;
            var entry = _viewModel.Entries.FirstOrDefault(l => l.Id == id);
            if (entry == null)
                return;

            _editingEntry = entry;
            EditPanelTitle.Text = "항목 편집";
            TitleBox.Text = entry.Title;
            KeywordsBox.Text = string.Join(", ", entry.Keywords);
            ContentBox.Text = entry.Content;
            EditPanel.Visibility = Visibility.Visible;
            TitleBox.Focus();
        }

        private async void DeleteEntry_Click(object sender, RoutedEventArgs e)
        {
            string id = (string)((Button)sender).Tag;
            var entry = _viewModel.Entries.FirstOrDefault(l => l.Id == id);
            if (entry == null)
                return;

            var result = MessageBox.Show(
                $"'{entry.Title}' 항목을 삭제할까요?",
                "로어 삭제",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;

            await _viewModel.DeleteAsync(entry);

            if (_editingEntry?.Id == id)
                HideEditPanel();

            RefreshList();
        }

        private async void SaveEditButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TitleBox.Text))
            {
                MessageBox.Show("항목 이름을 입력해주세요.", "알림");
                return;
            }

            await _viewModel.SaveEntryAsync(_editingEntry, TitleBox.Text, KeywordsBox.Text, ContentBox.Text);
            HideEditPanel();
            RefreshList();
        }

        private void CancelEditButton_Click(object sender, RoutedEventArgs e)
            => HideEditPanel();

        private void HideEditPanel()
        {
            _editingEntry = null;
            EditPanel.Visibility = Visibility.Collapsed;
        }
    }
}
