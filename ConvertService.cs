using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;

namespace MCloudServer
{
    public class ConvertService
    {
        private readonly ILogger<ConvertService> logger;

        private readonly ConcurrentDictionary<string, ConvTask> tasks;
        // key: id + "-" + conv_name

        public ConvertService(ILogger<ConvertService> logger)
        {
            this.logger = logger;
            this.tasks = new ConcurrentDictionary<string, ConvTask>();
        }

        public async Task<ConvResult> GetConverted(DbCtx dbctx, Track track, MCloudConfig.Converter conv)
        {
            var taskKey = track.id + "-" + conv.Name;
            var task = tasks.GetOrAdd(taskKey, (key) =>
            {
                return new ConvTask(dbctx, track, conv, logger);
            });
            try
            {
                logger.LogInformation("'{taskKey}' start convert", taskKey);
                await task.Run(out var alreadyRunning);
                logger.LogInformation("'{taskKey}' end (already existed = {val})", taskKey, alreadyRunning);
                return new ConvResult
                {
                    TrackFile = task.TrackFile,
                    AlreadyExisted = alreadyRunning
                };
            }
            finally
            {
                tasks.TryRemove(taskKey, out _);
            }
        }

        public struct ConvResult
        {
            public TrackFile TrackFile;
            public bool AlreadyExisted;
        }

        class ConvTask
        {
            public DbCtx Dbctx { get; }
            public Track Track { get; }
            public MCloudConfig.Converter Conv { get; }


            public TrackFile TrackFile { private set; get; }
            public string OutputUrl { private set; get; }

            Task runningTask;
            private readonly ILogger<ConvertService> logger;

            public ConvTask(DbCtx dbctx, Track track, MCloudConfig.Converter conv, ILogger<ConvertService> logger)
            {
                Dbctx = dbctx;
                Track = track;
                Conv = conv;
                this.logger = logger;
            }

            public Task Run() => Run(out _);

            public Task Run(out bool alreadyRunning)
            {
                alreadyRunning = true;
                if (runningTask != null) return runningTask;
                lock (this)
                {
                    if (runningTask != null) return runningTask;
                    alreadyRunning = false;
                    runningTask = Task.Run(RunCore);
                    return runningTask;
                }
            }

            public async Task RunCore()
            {
                var url = Track.url;
                var outputUrl = url + "." + Conv.Name;
                OutputUrl = outputUrl;

                var inputPath = Dbctx.MCloudConfig.ResolveStoragePath(url);
                var outputPath = Dbctx.MCloudConfig.ResolveStoragePath(outputUrl);

                string convCmdline = Conv.GetCommandLine(inputPath, outputPath);

                var debug = Dbctx.MCloudConfig.ConverterDebug;

                bool windows = Environment.OSVersion.Platform == PlatformID.Win32NT;
                var psi = new ProcessStartInfo(windows ? "cmd.exe" : "bash")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = debug,
                    RedirectStandardOutput = debug
                };
                psi.ArgumentList.Add(windows ? "/c" : "-c");
                psi.ArgumentList.Add(convCmdline);
                logger.LogInformation("run cmdline: {cmdline}", convCmdline);
                var proc = Process.Start(psi);

                if (debug)
                {
                    proc.OutputDataReceived += (s, e) =>
                    {
                        logger.LogInformation("proc out: {msg}", e.Data);
                    };
                    proc.ErrorDataReceived += (s, e) =>
                    {
                        logger.LogInformation("proc err: {msg}", e.Data);
                    };
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                }

                proc.WaitForExit();

                if (proc.ExitCode != 0)
                    throw new Exception("converter process exited with code " + proc.ExitCode);

                var trackFile = new TrackFile
                {
                    Bitrate = Conv.Bitrate,
                    ConvName = Conv.Name,
                    Format = Conv.Format,
                    Size = new FileInfo(outputPath).Length
                };
                TrackFile = trackFile;

                if (Dbctx.App.StorageService.Mode != StorageMode.Direct)
                {
                    logger.LogInformation("'{id}-{conv}' uploading to storage service...", Track.id, Conv.Name);
                    await Dbctx.App.StorageService.PutFile(new RequestUploadOptions
                    {
                        DestFilePath = Dbctx.MCloudConfig.GetStoragePath(outputUrl),
                        Length = trackFile.Size
                    }, outputPath);
                }
            }
        }
    }
}
