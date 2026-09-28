using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AICharacterChat.Domain.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AICharacterChat.Presentation.ViewModels
{
    public class LoreBookViewModel : ObservableObject
    {
        private readonly Character _character;
        private readonly Func<Task> _saveAsync;

        public ObservableCollection<LoreEntry> Entries { get; } = new();

        public LoreBookViewModel(Character character, Func<Task> saveAsync)
        {
            _character = character;
            _saveAsync = saveAsync;
            Refresh();
        }

        public async Task ToggleAsync(LoreEntry entry, bool isEnabled)
        {
            entry.IsEnabled = isEnabled;
            await _saveAsync();
            Refresh();
        }

        public async Task SaveEntryAsync(LoreEntry? existing, string title, string keywordsText, string content)
        {
            var keywords = keywordsText
                .Split(',')
                .Select(k => k.Trim())
                .Where(k => !string.IsNullOrEmpty(k))
                .ToList();

            if (existing == null)
            {
                _character.Lore.Add(new LoreEntry
                {
                    Title = title.Trim(),
                    Keywords = keywords,
                    Content = content.Trim(),
                    IsEnabled = true
                });
            }
            else
            {
                existing.Title = title.Trim();
                existing.Keywords = keywords;
                existing.Content = content.Trim();
            }

            await _saveAsync();
            Refresh();
        }

        public async Task DeleteAsync(LoreEntry entry)
        {
            _character.Lore.Remove(entry);
            await _saveAsync();
            Refresh();
        }

        private void Refresh()
        {
            Entries.Clear();
            foreach (var entry in _character.Lore)
                Entries.Add(entry);
        }
    }
}
