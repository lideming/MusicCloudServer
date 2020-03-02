using System;
using System.Security.Cryptography;

namespace MCloudServer
{
    public class AppService
    {
        public AppService(MCloudConfig config, StorageService storageService)
        {
            Config = config;
            StorageService = storageService;
            StartTime = DateTime.Now;

            signKey = new byte[16];
            RandomNumberGenerator.Fill(signKey);
        }

        public MCloudConfig Config { get; }

        public StorageService StorageService { get; }

        public DateTime StartTime { get; }

        public TimeSpan GetUptime() => DateTime.Now - StartTime;

        byte[] signKey;

        public string SignTag(string str) => Utils.SignTag(str, signKey);
    }
}