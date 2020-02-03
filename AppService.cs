using System;

namespace MCloudServer
{
    public class AppService
    {
        public AppService(MCloudConfig config)
        {
            Config = config;
            StartTime = DateTime.Now;
        }

        public MCloudConfig Config { get; }

        public DateTime StartTime { get; }

        public TimeSpan GetUptime() => DateTime.Now - StartTime;
    }
}