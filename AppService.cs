using System;
using System.Security.Cryptography;

namespace MCloudServer
{
    public class AppService
    {
        public AppService(MCloudConfig config, StorageService storageService, ConvertService convertService, FileService fileService)
        {
            Config = config;
            StorageService = storageService;
            ConvertService = convertService;
            FileService = fileService;
            StartTime = DateTime.Now;

            signKey = new byte[16];
            RandomNumberGenerator.Fill(signKey);
        }

        public MCloudConfig Config { get; }

        public StorageService StorageService { get; }

        public ConvertService ConvertService { get; }

        public FileService FileService { get; }

        public DateTime StartTime { get; }

        public TimeSpan GetUptime() => DateTime.Now - StartTime;

        byte[] signKey;

        public string SignTag(string str) => Utils.SignTag(str, signKey);
        public string SignToken(string[] strs, TimeSpan ttl) => Utils.SignToken(strs, signKey, ttl);
        public string[] ExtractToken(string token) => Utils.ExtractToken(token, signKey);

        public string ResolveStoragePath(string path) => Config.ResolveStoragePath(path);
        public bool TryResolveStoragePath(string path, out string fsPath) => Config.TryResolveStoragePath(path, out fsPath);

        public string GetFullUrlFromStoragePath(string path) {
            if (!path.StartsWith("storage/")) throw new Exception("Unknown prefix");
            if (string.IsNullOrEmpty(Config.StorageUrlBase)) {
                // Well, this is not "full" URL
                return "/api/storage/" + path.Substring(8);
            } else {
                return Config.StorageUrlBase + path.Substring(8);
            }
        }
    }
}
