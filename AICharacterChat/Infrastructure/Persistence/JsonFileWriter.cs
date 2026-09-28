using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AICharacterChat.Infrastructure.Persistence
{
    internal static class JsonFileWriter
    {
        private static readonly SemaphoreSlim SaveLock = new(1, 1);

        public static async Task WriteAtomicallyAsync(
            string path,
            string content,
            CancellationToken cancellationToken)
        {
            string directory = Path.GetDirectoryName(path)
                ?? throw new InvalidOperationException("저장 경로가 올바르지 않습니다.");
            Directory.CreateDirectory(directory);

            string tempPath = $"{path}.tmp.{Guid.NewGuid():N}";
            await SaveLock.WaitAsync(cancellationToken);
            try
            {
                await File.WriteAllTextAsync(tempPath, content, cancellationToken);

                if (File.Exists(path))
                {
                    string backupPath = path + ".previous";
                    File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(tempPath, path);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                SaveLock.Release();
            }
        }
    }
}
