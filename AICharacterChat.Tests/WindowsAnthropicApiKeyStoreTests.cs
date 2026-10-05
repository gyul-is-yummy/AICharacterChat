using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AICharacterChat.Application.Models;
using AICharacterChat.Infrastructure.Persistence;
using Xunit;

namespace AICharacterChat.Tests
{
    public class WindowsAnthropicApiKeyStoreTests : IDisposable
    {
        private readonly string _root;
        private readonly AppDataPaths _paths;
        private readonly WindowsAnthropicApiKeyStore _store;

        public WindowsAnthropicApiKeyStoreTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "AICharacterChatTests", Guid.NewGuid().ToString("N"));
            _paths = new AppDataPaths(_root, Path.Combine(_root, "missing-worlds.json"));
            _store = new WindowsAnthropicApiKeyStore(_paths);
        }

        [Fact]
        public async Task MissingFileReturnsMissingAndNull()
        {
            Assert.Equal(AnthropicApiKeyStatus.Missing, await _store.GetStatusAsync());
            Assert.Null(await _store.GetApiKeyAsync());
        }

        [Fact]
        public async Task SaveThenLoadReturnsAvailableKey()
        {
            await _store.SaveAsync("test-key");

            Assert.Equal(AnthropicApiKeyStatus.Available, await _store.GetStatusAsync());
            Assert.Equal("test-key", await _store.GetApiKeyAsync());
            Assert.True(File.Exists(_paths.CredentialsPath));
            Assert.DoesNotContain("test-key", await File.ReadAllTextAsync(_paths.CredentialsPath));
        }

        [Fact]
        public async Task ReplaceReturnsLatestKey()
        {
            await _store.SaveAsync("first-key");
            await _store.SaveAsync("second-key");

            Assert.Equal("second-key", await _store.GetApiKeyAsync());
        }

        [Fact]
        public async Task DeleteRemovesStoredKey()
        {
            await _store.SaveAsync("test-key");

            await _store.DeleteAsync();

            Assert.Equal(AnthropicApiKeyStatus.Missing, await _store.GetStatusAsync());
            Assert.Null(await _store.GetApiKeyAsync());
            Assert.False(File.Exists(_paths.CredentialsPath));
        }

        [Fact]
        public async Task CorruptedBlobReturnsUnreadableAndSafeFailure()
        {
            Directory.CreateDirectory(_root);
            await File.WriteAllBytesAsync(_paths.CredentialsPath, [1, 2, 3, 4, 5]);

            Assert.Equal(AnthropicApiKeyStatus.Unreadable, await _store.GetStatusAsync());
            var ex = await Assert.ThrowsAsync<AnthropicApiKeyReadException>(() => _store.GetApiKeyAsync());
            Assert.Contains("읽을 수 없습니다", ex.Message);
            Assert.DoesNotContain("super-secret-test-key", ex.Message);
        }

        [Fact]
        public async Task SaveTouchesCredentialFileOnly()
        {
            await _store.SaveAsync("test-key");

            var entries = Directory.GetFileSystemEntries(_root).Select(Path.GetFileName).ToList();
            Assert.Equal(["credentials.dat"], entries);
            Assert.False(File.Exists(_paths.SettingsPath));
            Assert.False(Directory.Exists(_paths.DataDirectory));
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }
}
