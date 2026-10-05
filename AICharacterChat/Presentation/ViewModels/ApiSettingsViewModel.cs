using System;
using System.Threading.Tasks;
using AICharacterChat.Application.Interfaces;
using AICharacterChat.Application.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AICharacterChat.Presentation.ViewModels
{
    public sealed class ApiSettingsViewModel : ObservableObject
    {
        private readonly IAnthropicApiKeyStore _apiKeyStore;
        private AnthropicApiKeyStatus _apiKeyStatus = AnthropicApiKeyStatus.Missing;
        private bool _isSaving;
        private bool _isDeleting;
        private string _errorMessage = "";

        public ApiSettingsViewModel(IAnthropicApiKeyStore apiKeyStore)
        {
            _apiKeyStore = apiKeyStore;
        }

        public AnthropicApiKeyStatus ApiKeyStatus
        {
            get => _apiKeyStatus;
            private set
            {
                if (SetProperty(ref _apiKeyStatus, value))
                    OnPropertyChanged(nameof(StatusText));
            }
        }

        public string StatusText => ApiKeyStatus switch
        {
            AnthropicApiKeyStatus.Available => "API Key가 저장되어 있습니다.",
            AnthropicApiKeyStatus.Unreadable => "저장된 API Key를 읽을 수 없습니다. 새 키를 저장하거나 기존 데이터를 삭제해 주세요.",
            _ => "API Key가 설정되어 있지 않습니다."
        };

        public bool IsSaving
        {
            get => _isSaving;
            private set
            {
                if (SetProperty(ref _isSaving, value))
                    OnPropertyChanged(nameof(IsBusy));
            }
        }

        public bool IsDeleting
        {
            get => _isDeleting;
            private set
            {
                if (SetProperty(ref _isDeleting, value))
                    OnPropertyChanged(nameof(IsBusy));
            }
        }

        public bool IsBusy => IsSaving || IsDeleting;

        public string ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                if (SetProperty(ref _errorMessage, value))
                    OnPropertyChanged(nameof(HasErrorMessage));
            }
        }

        public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

        public async Task InitializeAsync()
        {
            await RefreshStatusAsync();
        }

        public async Task<bool> SaveAsync(string? apiKey)
        {
            if (IsBusy)
                return false;

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                ErrorMessage = "API Key를 입력해 주세요.";
                return false;
            }

            IsSaving = true;
            try
            {
                await _apiKeyStore.SaveAsync(apiKey);
                ErrorMessage = "";
                await RefreshStatusAsync();
                return ApiKeyStatus == AnthropicApiKeyStatus.Available;
            }
            catch (Exception)
            {
                ErrorMessage = "API Key 저장에 실패했습니다.";
                await RefreshStatusAsync();
                return false;
            }
            finally
            {
                IsSaving = false;
            }
        }

        public async Task<bool> DeleteAsync()
        {
            if (IsBusy)
                return false;

            IsDeleting = true;
            try
            {
                await _apiKeyStore.DeleteAsync();
                ErrorMessage = "";
                await RefreshStatusAsync();
                return ApiKeyStatus == AnthropicApiKeyStatus.Missing;
            }
            catch (Exception)
            {
                ErrorMessage = "저장된 API Key 삭제에 실패했습니다.";
                await RefreshStatusAsync();
                return false;
            }
            finally
            {
                IsDeleting = false;
            }
        }

        private async Task RefreshStatusAsync()
        {
            ApiKeyStatus = await _apiKeyStore.GetStatusAsync();
        }
    }
}
