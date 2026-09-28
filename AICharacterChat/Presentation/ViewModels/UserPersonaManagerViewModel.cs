using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AICharacterChat.Domain.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AICharacterChat.Presentation.ViewModels
{
    public class UserPersonaManagerViewModel : ObservableObject
    {
        private readonly World _world;
        private readonly Func<Task> _saveAsync;

        public ObservableCollection<UserPersona> UserPersonas { get; } = new();

        public UserPersonaManagerViewModel(World world, Func<Task> saveAsync)
        {
            _world = world;
            _saveAsync = saveAsync;
            Refresh();
        }

        public async Task AddAsync(UserPersona persona)
        {
            _world.UserPersonas.Add(persona);
            await _saveAsync();
            Refresh();
        }

        public async Task UpdateAsync(UserPersona target, UserPersona source)
        {
            target.Name = source.Name;
            target.Appearance = source.Appearance;
            target.Personality = source.Personality;
            target.AdditionalInfo = source.AdditionalInfo;
            await _saveAsync();
            Refresh();
        }

        public async Task DeleteAsync(UserPersona persona)
        {
            if (_world.UserPersonas.Count <= 1)
                return;

            _world.UserPersonas.Remove(persona);
            foreach (var session in _world.ChatSessions.Where(s => s.UserPersonaId == persona.Id))
                session.UserPersonaId = _world.UserPersonas[0].Id;

            await _saveAsync();
            Refresh();
        }

        private void Refresh()
        {
            UserPersonas.Clear();
            foreach (var persona in _world.UserPersonas)
                UserPersonas.Add(persona);
        }
    }
}
