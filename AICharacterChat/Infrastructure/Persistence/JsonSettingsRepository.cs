using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AICharacterChat.Application.Interfaces;
using AICharacterChat.Domain.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AICharacterChat.Infrastructure.Persistence
{
    public class JsonSettingsRepository : ISettingsRepository
    {
        private readonly AppDataPaths _paths;

        public JsonSettingsRepository(AppDataPaths paths)
        {
            _paths = paths;
        }

        public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
        {
            _paths.EnsureDirectories();
            if (!File.Exists(_paths.SettingsPath))
            {
                var migrated = TryLoadLegacySettings();
                await SaveAsync(migrated, cancellationToken);
                return migrated;
            }

            string json = await File.ReadAllTextAsync(_paths.SettingsPath, cancellationToken);
            return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
        }

        public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            _paths.EnsureDirectories();
            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            await JsonFileWriter.WriteAtomicallyAsync(_paths.SettingsPath, json, cancellationToken);
        }

        private AppSettings TryLoadLegacySettings()
        {
            if (!File.Exists(_paths.LegacyWorldsPath))
                return new AppSettings();

            try
            {
                string json = File.ReadAllText(_paths.LegacyWorldsPath);
                string? selectedModel = JObject.Parse(json)["SelectedModel"]?.Value<string>();
                return string.IsNullOrWhiteSpace(selectedModel)
                    ? new AppSettings()
                    : new AppSettings { SelectedModel = selectedModel };
            }
            catch
            {
                return new AppSettings();
            }
        }
    }
}
