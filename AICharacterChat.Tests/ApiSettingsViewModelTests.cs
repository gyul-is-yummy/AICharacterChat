using System;
using System.Threading;
using System.Threading.Tasks;
using AICharacterChat.Application.Interfaces;
using AICharacterChat.Application.Models;
using AICharacterChat.Presentation.ViewModels;
using Xunit;

namespace AICharacterChat.Tests
{
    public class ApiSettingsViewModelTests
    {
        [Fact]
        public async Task InitializeShowsMissingStatus()
        {
            var store = new FakeApiKeyStore { Status = AnthropicApiKeyStatus.Missing };
            var viewModel = new ApiSettingsViewModel(store);

            await viewModel.InitializeAsync();

            Assert.Equal(AnthropicApiKeyStatus.Missing, viewModel.ApiKeyStatus);
            Assert.Contains("설정되어 있지", viewModel.StatusText);
        }

        [Fact]
        public async Task InitializeShowsAvailableStatus()
        {
            var store = new FakeApiKeyStore { Status = AnthropicApiKeyStatus.Available };
            var viewModel = new ApiSettingsViewModel(store);

            await viewModel.InitializeAsync();

            Assert.Equal(AnthropicApiKeyStatus.Available, viewModel.ApiKeyStatus);
            Assert.Contains("저장되어", viewModel.StatusText);
        }

        [Fact]
        public async Task InitializeShowsUnreadableStatus()
        {
            var store = new FakeApiKeyStore { Status = AnthropicApiKeyStatus.Unreadable };
            var viewModel = new ApiSettingsViewModel(store);

            await viewModel.InitializeAsync();

            Assert.Equal(AnthropicApiKeyStatus.Unreadable, viewModel.ApiKeyStatus);
            Assert.Contains("읽을 수 없습니다", viewModel.StatusText);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task SaveRejectsEmptyOrWhitespace(string apiKey)
        {
            var store = new FakeApiKeyStore();
            var viewModel = new ApiSettingsViewModel(store);

            bool saved = await viewModel.SaveAsync(apiKey);

            Assert.False(saved);
            Assert.Equal(0, store.SaveCount);
            Assert.True(viewModel.HasErrorMessage);
            Assert.Contains("입력", viewModel.ErrorMessage);
        }

        [Fact]
        public async Task SaveSuccessUpdatesStatusToAvailable()
        {
            var store = new FakeApiKeyStore();
            var viewModel = new ApiSettingsViewModel(store);

            bool saved = await viewModel.SaveAsync("test-key");

            Assert.True(saved);
            Assert.Equal("test-key", store.SavedKey);
            Assert.Equal(AnthropicApiKeyStatus.Available, viewModel.ApiKeyStatus);
            Assert.False(viewModel.HasErrorMessage);
        }

        [Fact]
        public async Task ReplaceSuccessStoresLatestKey()
        {
            var store = new FakeApiKeyStore();
            var viewModel = new ApiSettingsViewModel(store);

            await viewModel.SaveAsync("first-key");
            bool saved = await viewModel.SaveAsync("second-key");

            Assert.True(saved);
            Assert.Equal("second-key", store.SavedKey);
            Assert.Equal(2, store.SaveCount);
        }

        [Fact]
        public async Task DeleteSuccessUpdatesStatusToMissing()
        {
            var store = new FakeApiKeyStore { Status = AnthropicApiKeyStatus.Available };
            var viewModel = new ApiSettingsViewModel(store);
            await viewModel.InitializeAsync();

            bool deleted = await viewModel.DeleteAsync();

            Assert.True(deleted);
            Assert.Equal(AnthropicApiKeyStatus.Missing, viewModel.ApiKeyStatus);
            Assert.False(viewModel.HasErrorMessage);
        }

        [Fact]
        public async Task SaveFailurePreservesActualStatusAndShowsSafeError()
        {
            var store = new FakeApiKeyStore
            {
                Status = AnthropicApiKeyStatus.Missing,
                SaveException = new InvalidOperationException("super-secret-test-key")
            };
            var viewModel = new ApiSettingsViewModel(store);

            bool saved = await viewModel.SaveAsync("super-secret-test-key");

            Assert.False(saved);
            Assert.Equal(AnthropicApiKeyStatus.Missing, viewModel.ApiKeyStatus);
            Assert.True(viewModel.HasErrorMessage);
            Assert.DoesNotContain("super-secret-test-key", viewModel.ErrorMessage);
        }

        [Fact]
        public async Task DeleteFailurePreservesActualStatusAndShowsError()
        {
            var store = new FakeApiKeyStore
            {
                Status = AnthropicApiKeyStatus.Available,
                DeleteException = new InvalidOperationException("delete failed")
            };
            var viewModel = new ApiSettingsViewModel(store);
            await viewModel.InitializeAsync();

            bool deleted = await viewModel.DeleteAsync();

            Assert.False(deleted);
            Assert.Equal(AnthropicApiKeyStatus.Available, viewModel.ApiKeyStatus);
            Assert.True(viewModel.HasErrorMessage);
        }

        private sealed class FakeApiKeyStore : IAnthropicApiKeyStore
        {
            public AnthropicApiKeyStatus Status { get; set; } = AnthropicApiKeyStatus.Missing;
            public string? SavedKey { get; private set; }
            public int SaveCount { get; private set; }
            public Exception? SaveException { get; set; }
            public Exception? DeleteException { get; set; }

            public Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult<string?>(Status == AnthropicApiKeyStatus.Available ? SavedKey : null);

            public Task<AnthropicApiKeyStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(Status);

            public Task SaveAsync(string apiKey, CancellationToken cancellationToken = default)
            {
                if (SaveException != null)
                    throw SaveException;

                SaveCount++;
                SavedKey = apiKey;
                Status = AnthropicApiKeyStatus.Available;
                return Task.CompletedTask;
            }

            public Task DeleteAsync(CancellationToken cancellationToken = default)
            {
                if (DeleteException != null)
                    throw DeleteException;

                SavedKey = null;
                Status = AnthropicApiKeyStatus.Missing;
                return Task.CompletedTask;
            }
        }
    }
}
