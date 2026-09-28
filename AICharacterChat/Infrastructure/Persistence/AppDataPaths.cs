using System;
using System.IO;

namespace AICharacterChat.Infrastructure.Persistence
{
    public class AppDataPaths
    {
        public string RootDirectory { get; }
        public string DataDirectory => Path.Combine(RootDirectory, "data");
        public string WorldStorePath => Path.Combine(DataDirectory, "worlds.json");
        public string SettingsPath => Path.Combine(RootDirectory, "settings.json");
        public string LegacyWorldsPath { get; }

        public AppDataPaths()
            : this(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AICharacterChat"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "worlds.json"))
        {
        }

        public AppDataPaths(string rootDirectory, string legacyWorldsPath)
        {
            RootDirectory = rootDirectory;
            LegacyWorldsPath = legacyWorldsPath;
        }

        public void EnsureDirectories()
        {
            Directory.CreateDirectory(RootDirectory);
            Directory.CreateDirectory(DataDirectory);
        }
    }
}
