using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AICharacterChat.Application.Interfaces;
using AICharacterChat.Application.Models;

namespace AICharacterChat.Infrastructure.Persistence
{
    public sealed class WindowsAnthropicApiKeyStore : IAnthropicApiKeyStore
    {
        private const string UnreadableMessage = "저장된 Anthropic API Key를 읽을 수 없습니다. API 설정에서 키를 다시 저장해 주세요.";
        private readonly AppDataPaths _paths;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public WindowsAnthropicApiKeyStore(AppDataPaths paths)
        {
            _paths = paths;
        }

        public async Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (!File.Exists(_paths.CredentialsPath))
                    return null;

                return await ReadApiKeyCoreAsync(cancellationToken);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<AnthropicApiKeyStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (!File.Exists(_paths.CredentialsPath))
                    return AnthropicApiKeyStatus.Missing;

                try
                {
                    string apiKey = await ReadApiKeyCoreAsync(cancellationToken);
                    return string.IsNullOrWhiteSpace(apiKey)
                        ? AnthropicApiKeyStatus.Unreadable
                        : AnthropicApiKeyStatus.Available;
                }
                catch (AnthropicApiKeyReadException)
                {
                    return AnthropicApiKeyStatus.Unreadable;
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task SaveAsync(string apiKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API Key를 입력해 주세요.", nameof(apiKey));

            byte[] plainBytes = Encoding.UTF8.GetBytes(apiKey.Trim());
            byte[] protectedBytes = ProtectedData.Protect(
                plainBytes,
                optionalEntropy: null,
                DataProtectionScope.CurrentUser);

            await _lock.WaitAsync(cancellationToken);
            try
            {
                await WriteAtomicallyAsync(_paths.CredentialsPath, protectedBytes, cancellationToken);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task DeleteAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(_paths.CredentialsPath))
                    File.Delete(_paths.CredentialsPath);
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<string> ReadApiKeyCoreAsync(CancellationToken cancellationToken)
        {
            try
            {
                byte[] protectedBytes = await File.ReadAllBytesAsync(_paths.CredentialsPath, cancellationToken);
                byte[] plainBytes = ProtectedData.Unprotect(
                    protectedBytes,
                    optionalEntropy: null,
                    DataProtectionScope.CurrentUser);
                string apiKey = Encoding.UTF8.GetString(plainBytes);
                if (string.IsNullOrWhiteSpace(apiKey))
                    throw new AnthropicApiKeyReadException(UnreadableMessage);

                return apiKey;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (AnthropicApiKeyReadException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException ||
                                       ex is UnauthorizedAccessException ||
                                       ex is CryptographicException ||
                                       ex is ArgumentException)
            {
                throw new AnthropicApiKeyReadException(UnreadableMessage, ex);
            }
        }

        private static async Task WriteAtomicallyAsync(
            string path,
            byte[] content,
            CancellationToken cancellationToken)
        {
            string directory = Path.GetDirectoryName(path)
                ?? throw new InvalidOperationException("저장 경로가 올바르지 않습니다.");
            Directory.CreateDirectory(directory);

            string tempPath = $"{path}.tmp.{Guid.NewGuid():N}";
            try
            {
                await File.WriteAllBytesAsync(tempPath, content, cancellationToken);
                File.Move(tempPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
    }
}
