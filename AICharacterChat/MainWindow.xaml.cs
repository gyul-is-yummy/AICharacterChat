using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AICharacterChat
{
    public partial class MainWindow : Window
    {
        // API 키는 환경 변수 ANTHROPIC_API_KEY 에 설정하세요.
        // 예) 시스템 환경 변수 또는 .env 파일 (절대 코드에 직접 입력하지 마세요)
        private static readonly string API_KEY =
            Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "";

        private static readonly (string Id, string Label)[] AvailableModels =
        {
            ("claude-haiku-4-5-20251001", "Haiku 4.5 (빠름/저렴)"),
            ("claude-sonnet-4-6",         "Sonnet 4.6 (균형)"),
            ("claude-opus-4-6",           "Opus 4.6 (고성능)"),
        };

        private WorldManager _manager;
        private static readonly HttpClient _httpClient = new();

        public MainWindow()
        {
            InitializeComponent();
            _manager = WorldManager.Load();
            InitModelComboBox();
            RefreshWorldList();
            RefreshCharacterList();
            LoadActiveCharacterChat();
        }

        private void InitModelComboBox()
        {
            ModelComboBox.SelectionChanged -= ModelComboBox_SelectionChanged;
            ModelComboBox.Items.Clear();
            int selectedIdx = 0;
            for (int i = 0; i < AvailableModels.Length; i++)
            {
                var (id, label) = AvailableModels[i];
                ModelComboBox.Items.Add(new ComboBoxItem { Content = label, Tag = id });
                if (id == _manager.SelectedModel) selectedIdx = i;
            }
            ModelComboBox.SelectedIndex = selectedIdx;
            ModelComboBox.SelectionChanged += ModelComboBox_SelectionChanged;
        }

        private void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModelComboBox.SelectedItem is ComboBoxItem item)
            {
                _manager.SelectedModel = (string)item.Tag;
                _manager.Save();
            }
        }

        // ═══════════════════════════════════════════
        // 세계관 목록
        // ═══════════════════════════════════════════

        private void RefreshWorldList()
        {
            WorldListPanel.Children.Clear();
            foreach (var world in _manager.Worlds)
                WorldListPanel.Children.Add(CreateWorldItem(world));
        }

        private Border CreateWorldItem(WorldProfile world)
        {
            bool isActive = world.Id == _manager.ActiveWorldId;

            var border = new Border
            {
                Background = isActive
                    ? new SolidColorBrush(Color.FromRgb(20, 40, 90))
                    : new SolidColorBrush(Color.FromRgb(15, 15, 40)),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 0, 3),
                Padding = new Thickness(5, 4, 3, 4),
                Cursor = Cursors.Hand
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 세계관 이름 (클릭 → 전환)
            var nameBtn = new Button
            {
                Content = world.Name,
                Background = Brushes.Transparent,
                Foreground = isActive
                    ? new SolidColorBrush(Color.FromRgb(170, 210, 255))
                    : new SolidColorBrush(Color.FromRgb(110, 140, 190)),
                BorderThickness = new Thickness(0),
                FontSize = 12,
                FontWeight = isActive ? FontWeights.Bold : FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(2, 0, 0, 0),
                Cursor = Cursors.Hand,
                Tag = world.Id
            };
            nameBtn.Click += WorldName_Click;
            Grid.SetColumn(nameBtn, 0);

            // ⚙ 편집 버튼
            var editBtn = new Button
            {
                Content = "⚙",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 130, 170)),
                BorderThickness = new Thickness(0),
                FontSize = 11,
                Padding = new Thickness(3, 0, 2, 0),
                Cursor = Cursors.Hand,
                Tag = world.Id,
                ToolTip = "세계관 설정 편집"
            };
            editBtn.Click += WorldEdit_Click;
            Grid.SetColumn(editBtn, 1);

            grid.Children.Add(nameBtn);
            grid.Children.Add(editBtn);
            border.Child = grid;
            return border;
        }

        // 세계관 전환
        private void WorldName_Click(object sender, RoutedEventArgs e)
        {
            string id = (string)((Button)sender).Tag;
            if (id == _manager.ActiveWorldId) return;
            _manager.ActiveWorldId = id;
            _manager.Save();
            RefreshWorldList();
            RefreshCharacterList();
            LoadActiveCharacterChat();
        }

        // 세계관 편집
        private void WorldEdit_Click(object sender, RoutedEventArgs e)
        {
            string id = (string)((Button)sender).Tag;
            var world = _manager.Worlds.FirstOrDefault(w => w.Id == id);
            if (world == null) return;

            var win = new WorldSettingsWindow(world);
            win.Owner = this;
            if (win.ShowDialog() != true) return;

            var result = win.ResultWorld;
            world.Name = result.Name;
            world.Genre = result.Genre;
            world.Era = result.Era;
            world.Description = result.Description;
            world.Rules = result.Rules;
            _manager.Save();
            RefreshWorldList();
        }

        // + 세계관 추가
        private void AddWorldButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new WorldSettingsWindow(new WorldProfile());
            win.Owner = this;
            if (win.ShowDialog() != true) return;

            var newWorld = win.ResultWorld;
            _manager.AddWorld(newWorld);
            _manager.ActiveWorldId = newWorld.Id;
            _manager.Save();
            RefreshWorldList();
            RefreshCharacterList();
            LoadActiveCharacterChat();
        }

        // ═══════════════════════════════════════════
        // 캐릭터 목록
        // ═══════════════════════════════════════════

        private void RefreshCharacterList()
        {
            CharacterListPanel.Children.Clear();
            var characters = _manager.ActiveWorld?.Characters ?? new();
            foreach (var profile in characters)
                CharacterListPanel.Children.Add(CreateCharacterItem(profile));
        }

        private Border CreateCharacterItem(CharacterProfile profile)
        {
            var world = _manager.ActiveWorld;
            bool isActive = profile.Id == world?.ActiveCharacterId;

            var border = new Border
            {
                Background = isActive
                    ? new SolidColorBrush(Color.FromRgb(70, 20, 140))
                    : new SolidColorBrush(Color.FromRgb(22, 18, 50)),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 0, 3),
                Padding = new Thickness(5, 4, 3, 4),
                Cursor = Cursors.Hand
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameBtn = new Button
            {
                Content = profile.Name,
                Background = Brushes.Transparent,
                Foreground = isActive ? Brushes.White
                    : new SolidColorBrush(Color.FromRgb(180, 160, 220)),
                BorderThickness = new Thickness(0),
                FontSize = 12,
                FontWeight = isActive ? FontWeights.Bold : FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(2, 0, 0, 0),
                Cursor = Cursors.Hand,
                Tag = profile.Id
            };
            nameBtn.Click += CharacterName_Click;
            Grid.SetColumn(nameBtn, 0);

            var settingsBtn = new Button
            {
                Content = "⚙",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 130, 190)),
                BorderThickness = new Thickness(0),
                FontSize = 11,
                Padding = new Thickness(3, 0, 2, 0),
                Cursor = Cursors.Hand,
                Tag = profile.Id,
                ToolTip = "캐릭터 설정 편집"
            };
            settingsBtn.Click += CharacterSettings_Click;
            Grid.SetColumn(settingsBtn, 1);

            // 로어북 버튼
            var loreBtn = new Button
            {
                Content = "📖",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 130, 190)),
                BorderThickness = new Thickness(0),
                FontSize = 11,
                Padding = new Thickness(2, 0, 2, 0),
                Cursor = Cursors.Hand,
                Tag = profile.Id,
                ToolTip = "로어북"
            };
            loreBtn.Click += CharacterLoreBook_Click;
            Grid.SetColumn(loreBtn, 2);

            // 대화 기록 초기화 버튼
            var clearBtn = new Button
            {
                Content = "↺",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 130, 190)),
                BorderThickness = new Thickness(0),
                FontSize = 13,
                Padding = new Thickness(2, 0, 2, 0),
                Cursor = Cursors.Hand,
                Tag = profile.Id,
                ToolTip = "대화 기록 초기화"
            };
            clearBtn.Click += CharacterClearHistory_Click;
            Grid.SetColumn(clearBtn, 3);

            var deleteBtn = new Button
            {
                Content = "🗑",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 130, 190)),
                BorderThickness = new Thickness(0),
                FontSize = 11,
                Padding = new Thickness(2, 0, 0, 0),
                Cursor = Cursors.Hand,
                Tag = profile.Id,
                ToolTip = "캐릭터 삭제"
            };
            deleteBtn.Click += CharacterDelete_Click;
            Grid.SetColumn(deleteBtn, 4);

            grid.Children.Add(nameBtn);
            grid.Children.Add(settingsBtn);
            grid.Children.Add(loreBtn);
            grid.Children.Add(clearBtn);
            grid.Children.Add(deleteBtn);
            border.Child = grid;
            return border;
        }

        // 캐릭터 전환
        private void CharacterName_Click(object sender, RoutedEventArgs e)
        {
            string id = (string)((Button)sender).Tag;
            var world = _manager.ActiveWorld;
            if (world == null || id == world.ActiveCharacterId) return;
            world.ActiveCharacterId = id;
            _manager.Save();
            RefreshCharacterList();
            LoadActiveCharacterChat();
        }

        // 캐릭터 설정 편집
        private void CharacterSettings_Click(object sender, RoutedEventArgs e)
        {
            string id = (string)((Button)sender).Tag;
            var world = _manager.ActiveWorld;
            var profile = world?.Characters.FirstOrDefault(c => c.Id == id);
            if (profile == null) return;

            var otherChars = world!.Characters.Where(c => c.Id != id).ToList();
            var win = new CharacterSettingsWindow(profile, otherChars, world, _manager);
            win.Owner = this;

            if (win.ShowDialog() != true) return;

            var r = win.ResultProfile;
            profile.Name = r.Name;
            profile.Age = r.Age;
            profile.Gender = r.Gender;   // ★ 추가
            profile.Job = r.Job;
            profile.Appearance = r.Appearance;
            profile.Personality = r.Personality;
            profile.Relationships = r.Relationships;
            profile.Etc = r.Etc;
            profile.Secret = r.Secret;
            profile.SpeechStyle = r.SpeechStyle;
            profile.Situation = r.Situation;
            profile.CustomFields = r.CustomFields;  // ★ 추가
            profile.SelectedUserProfileId = r.SelectedUserProfileId;
            _manager.Save();

            RefreshCharacterList();
            if (id == world.ActiveCharacterId)
                CharacterNameText.Text = profile.Name;
        }

        // 대화 기록 초기화
        private void CharacterClearHistory_Click(object sender, RoutedEventArgs e)
        {
            string id = (string)((Button)sender).Tag;
            var world = _manager.ActiveWorld;
            var profile = world?.Characters.FirstOrDefault(c => c.Id == id);
            if (profile == null) return;

            var confirm = MessageBox.Show(
                $"'{profile.Name}'과의 대화 기록을 초기화할까요?",
                "대화 기록 초기화",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            profile.ConversationHistory.Clear();
            _manager.Save();

            // 현재 보고 있는 캐릭터라면 채팅창도 비우기
            if (id == world!.ActiveCharacterId)
                ChatPanel.Children.Clear();
        }

        // 캐릭터 삭제
        private void CharacterDelete_Click(object sender, RoutedEventArgs e)
        {
            string id = (string)((Button)sender).Tag;
            var world = _manager.ActiveWorld;
            var profile = world?.Characters.FirstOrDefault(c => c.Id == id);
            if (profile == null) return;

            if (world!.Characters.Count <= 1)
            {
                MessageBox.Show("캐릭터가 한 명 이상 있어야 합니다.", "알림");
                return;
            }

            var confirm = MessageBox.Show(
                $"'{profile.Name}'을(를) 삭제할까요?\n대화 기록도 함께 삭제됩니다.",
                "캐릭터 삭제",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            _manager.RemoveCharacter(id);
            RefreshCharacterList();
            LoadActiveCharacterChat();
        }

        // + 캐릭터 추가
        private void AddCharacterButton_Click(object sender, RoutedEventArgs e)
        {
            var world = _manager.ActiveWorld;
            var otherChars = world?.Characters.ToList() ?? new();
            var win = new CharacterSettingsWindow(new CharacterProfile(), otherChars, world, _manager);

            win.Owner = this;

            if (win.ShowDialog() != true) return;

            var newProfile = win.ResultProfile;
            _manager.AddCharacter(newProfile);
            _manager.ActiveWorld!.ActiveCharacterId = newProfile.Id;
            _manager.Save();
            RefreshCharacterList();
            LoadActiveCharacterChat();
        }

        // ═══════════════════════════════════════════
        // 채팅 영역
        // ═══════════════════════════════════════════

        private void LoadActiveCharacterChat()
        {
            var profile = _manager.ActiveWorld?.ActiveCharacter;
            if (profile == null)
            {
                CharacterNameText.Text = "";
                ChatPanel.Children.Clear();
                return;
            }

            CharacterNameText.Text = profile.Name;
            ChatPanel.Children.Clear();

            foreach (var msg in profile.ConversationHistory)
            {
                if (msg.Role == "user")
                    AddBubble(UnwrapInput(msg.Content), isUser: true);
                else
                    AddBubble(msg.Content, isUser: false);
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
            => await SendMessage();

        private async void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
            {
                e.Handled = true;
                await SendMessage();
            }
        }

        private async System.Threading.Tasks.Task SendMessage()
        {
            var profile = _manager.ActiveWorld?.ActiveCharacter;
            if (profile == null) return;

            string userInput = InputBox.Text.Trim();
            if (string.IsNullOrEmpty(userInput)) return;

            InputBox.Text = "";
            SendButton.IsEnabled = false;

            AddBubble(userInput, isUser: true);

            string wrappedInput = $"""
                [현재 상황 서술]
                {userInput}

                위 상황에서 {profile.Name}으로서 반응해주세요.
                행동 묘사와 대사를 함께 포함하여 소설 문체로 답하세요.
                """;

            profile.ConversationHistory.Add(new ChatMessage("user", wrappedInput));

            var typingBubble = AddBubble("...", isUser: false);
            string reply = await CallClaudeAPI(profile, userInput);

            ChatPanel.Children.Remove(typingBubble);
            AddBubble(reply, isUser: false);

            // 오류 응답이면 기록 저장 안 함
            if (reply.StartsWith("(오류가 발생했습니다"))
            {
                // 방금 추가한 user 메시지도 다시 제거
                profile.ConversationHistory.RemoveAt(
                    profile.ConversationHistory.Count - 1);
            }
            else
            {
                profile.ConversationHistory.Add(new ChatMessage("assistant", reply));
                _manager.Save();
            }

            ChatScrollViewer.ScrollToBottom();
            SendButton.IsEnabled = true;
        }

        private async System.Threading.Tasks.Task<string> CallClaudeAPI(CharacterProfile profile, string userInput)
        {
            var world = _manager.ActiveWorld;
            var worldChars = world?.Characters ?? new();

            // 유저 프로필 찾기
            var userProfile = world?.UserProfiles
                .FirstOrDefault(u => u.Id == profile.SelectedUserProfileId)
                ?? world?.UserProfiles.FirstOrDefault();

            // 키워드 매칭 로어 필터링 (캐릭터별)
            var matchingLore = profile.Lore
                .Where(e => e.IsEnabled && e.Keywords.Any(k =>
                    !string.IsNullOrWhiteSpace(k) &&
                    userInput.Contains(k.Trim(), System.StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var requestBody = new
            {
                model = _manager.SelectedModel,
                max_tokens = 1024,
                system = profile.BuildSystemPrompt(world, worldChars, userProfile, matchingLore),
                messages = profile.ConversationHistory
            };

            string json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", API_KEY);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            try
            {
                var response = await _httpClient.PostAsync(
                    "https://api.anthropic.com/v1/messages", content);
                string respJson = await response.Content.ReadAsStringAsync();
                dynamic result = JsonConvert.DeserializeObject(respJson)!;
                return result.content[0].text;
            }
            catch
            {
                return "(오류가 발생했습니다. API 키를 확인해주세요.)";
            }
        }

        // ═══════════════════════════════════════════
        // UI 헬퍼
        // ═══════════════════════════════════════════

        private Border AddBubble(string text, bool isUser)
        {
            var bubble = new Border
            {
                Background = isUser
                    ? new SolidColorBrush(Color.FromRgb(123, 47, 247))
                    : new SolidColorBrush(Color.FromRgb(22, 33, 62)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(isUser ? 60 : 5, 4, isUser ? 5 : 60, 4),
                HorizontalAlignment = isUser
                    ? HorizontalAlignment.Right
                    : HorizontalAlignment.Left,
                Child = new TextBlock
                {
                    Text = text,
                    Foreground = Brushes.White,
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap
                }
            };
            ChatPanel.Children.Add(bubble);
            ChatScrollViewer.ScrollToBottom();
            return bubble;
        }

        // 캐릭터 로어북
        private void CharacterLoreBook_Click(object sender, RoutedEventArgs e)
        {
            string id = (string)((Button)sender).Tag;
            var profile = _manager.ActiveWorld?.Characters.FirstOrDefault(c => c.Id == id);
            if (profile == null) return;
            var win = new LoreBookWindow(profile, _manager);
            win.Owner = this;
            win.ShowDialog();
        }

        private string UnwrapInput(string wrapped)
        {
            wrapped = wrapped.Replace("\r\n", "\n");
            const string header = "[현재 상황 서술]\n";
            int start = wrapped.IndexOf(header);
            if (start < 0) return wrapped;
            start += header.Length;
            int end = wrapped.IndexOf("\n\n위 상황에서");
            return end < 0 ? wrapped[start..] : wrapped[start..end].Trim();
        }
    }
}