using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace MCloudServer
{
    public class ConvertService
    {
        private readonly ILogger<ConvertService> logger;
        private readonly IServiceProvider serviceProvider;

        private readonly ConcurrentDictionary<string, ConvTask> tasks;
        // key: id + "-" + conv_name

        private readonly Queue<(Track, MCloudConfig.Converter)> queue = new();

        public ConvertService(ILogger<ConvertService> logger, IServiceProvider serviceProvider)
        {
            this.logger = logger;
            this.serviceProvider = serviceProvider;
            this.tasks = new ConcurrentDictionary<string, ConvTask>();
        }

        public void AddBackgroundConvert(Track track, MCloudConfig.Converter conv)
        {
            lock(queue) {
                queue.Enqueue((track, conv));
                if (queue.Count == 1) {
                    BackgroundConvertRunner(queue.Dequeue());
                }
            }
        }

        public async void BackgroundConvertRunner((Track, MCloudConfig.Converter) current) {
            while(true) {
                try
                {
                    await GetConverted(current.Item1, current.Item2);
                }
                catch (System.Exception e)
                {
                    logger.LogError(e, "Background converting error");
                }

                lock(queue) {
                    logger.LogInformation("Background converting queue count: {count}", queue.Count);
                    if (queue.Count == 0) {
                        break;
                    }
                    current = queue.Dequeue();
                }
            }
        }

        public async Task<ConvResult> GetConverted(Track track, MCloudConfig.Converter conv)
        {
            var taskKey = track.id + "-" + conv.Name;
            var task = tasks.GetOrAdd(taskKey, (key) =>
            {
                return new ConvTask(serviceProvider.GetService<AppService>(), track, conv, logger);
            });
            try
            {
                logger.LogInformation("'{taskKey}' start convert", taskKey);
                await task.Run(out var alreadyRunning);
                if (!alreadyRunning)
                {
                    using (var scope = serviceProvider.CreateScope())
                    {
                        var dbctx = scope.ServiceProvider.GetService<DbCtx>();
                        dbctx.Attach(track);
                    RETRY:
                        // dbctx.Files.Add(task.TrackFile.File);
                        // dbctx.TrackFiles.Add(task.TrackFile);
                        track.files.Add(task.TrackFile);
                        if (await dbctx.FailedSavingChanges())
                        {
                            await dbctx.Entry(track).ReloadAsync();
                            goto RETRY;
                        }
                    }
                }
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
            public AppService App { get; }
            public Track Track { get; }
            public MCloudConfig.Converter Conv { get; }


            public TrackFile TrackFile { private set; get; }
            public string OutputUrl { private set; get; }

            Task runningTask;
            private readonly ILogger<ConvertService> logger;

            public ConvTask(AppService app, Track track, MCloudConfig.Converter conv, ILogger<ConvertService> logger)
            {
                App = app;
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
                var outputUrl = Track.ConvUrl(Conv.Name);
                OutputUrl = outputUrl;

                var inputPath = App.Config.ResolveStoragePath(url);
                var outputPath = App.Config.ResolveStoragePath(outputUrl);

                string convCmdline = Conv.GetCommandLine(inputPath, outputPath);

                var debug = App.Config.ConverterDebug;

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

                await proc.WaitForExitAsync();

                if (proc.ExitCode != 0)
                    throw new Exception("converter process exited with code " + proc.ExitCode);

                var trackFile = new TrackFile
                {
                    Bitrate = Conv.Bitrate,
                    ConvName = Conv.Name,
                    Format = Conv.Format,
                    File = new StoredFile
                    {
                        path = outputUrl,
                        size = new FileInfo(outputPath).Length
                    }
                };
                TrackFile = trackFile;

                if (App.StorageService.Mode != StorageMode.Direct)
                {
                    logger.LogInformation("'{id}-{conv}' uploading to storage service...", Track.id, Conv.Name);
                    await App.StorageService.PutFile(new RequestUploadOptions
                    {
                        DestFilePath = App.Config.GetStoragePath(outputUrl),
                        Length = trackFile.Size
                    }, outputPath);
                }
            }
        }
    }
}
