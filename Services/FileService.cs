using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace MCloudServer
{
    public class FileService
    {
        private readonly MCloudConfig _config;

        public FileService(MCloudConfig config)
        {
            _config = config;
        }

        public async Task<StoredFile> SaveFile(
            string pathFormat,
            Stream stream,
            long expectedSize = -1
        )
        {
            var filename = Guid.NewGuid().ToString("D");
            var internalPath = string.Format(pathFormat, filename);
            var fsPath = _config.ResolveStoragePath(internalPath);

            Directory.CreateDirectory(Path.GetDirectoryName(fsPath));

            string hash;
            long size;

            using (var fileStream = File.Create(fsPath))
            using (var sha256 = SHA256.Create())
            {
                var buffer = new byte[64 * 1024];
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
                }
                if (expectedSize != -1 && fileStream.Length != expectedSize)
                {
                    throw new Exception(
                        $"Stream size mismatch while saving file: expected {expectedSize}, actual {fileStream.Length}"
                    );
                }
                sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                hash = Convert.ToBase64String(sha256.Hash);
                size = fileStream.Length;
            }

            var storedFile = new StoredFile
            {
                path = internalPath,
                size = size,
                sha256 = hash
            };

            return storedFile;
        }

        public async Task FillHash(StoredFile file)
        {
            var fsPath = _config.ResolveStoragePath(file.path);
            using (var fs = File.OpenRead(fsPath))
            using (var sha256 = SHA256.Create())
            {
                var hash = await sha256.ComputeHashAsync(fs);
                file.sha256 = Convert.ToBase64String(hash);
            }
        }
    }
}
