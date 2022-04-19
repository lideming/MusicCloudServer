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

        private readonly Queue<string> queue = new();

        public ConvertService(ILogger<ConvertService> logger, IServiceProvider serviceProvider)
        {
            this.logger = logger;
            this.serviceProvider = serviceProvider;
            this.tasks = new ConcurrentDictionary<string, ConvTask>();
        }

        public void AddBackgroundConvert(Track track, MCloudConfig.Converter conv)
        {
            var task = GetConvTask(track, conv);
            lock (queue)
            {
                queue.Enqueue(task.Name);
                if (queue.Count == 1)
                {
                    BackgroundConvertRunner(queue.Peek());
                }
            }
        }

        public async void BackgroundConvertRunner(string current)
        {
            while (true)
            {
                try
                {
                    if (tasks.TryGetValue(current, out var task))
                    {
                        await task.Run();
                    }
                }
                catch (System.Exception e)
                {
                    logger.LogError(e, "Background converting error");
                }
                finally
                {
                    tasks.TryRemove(current, out _);
                }

                lock (queue)
                {
                    queue.Dequeue();
                    logger.LogInformation("Background converting queue count: {count}", queue.Count);
                    if (queue.Count == 0)
                    {
                        break;
                    }
                    current = queue.Peek();
                }
            }
        }

        public async Task<ConvResult> GetConverted(Track track, MCloudConfig.Converter conv)
        {
            var task = GetConvTask(track, conv);
            try
            {
                logger.LogInformation("'{taskKey}' start convert", task.Name);
                await task.Run(out var alreadyRunning);
                logger.LogInformation("'{taskKey}' end (already existed = {val})", task.Name, alreadyRunning);
                return new ConvResult
                {
                    TrackFile = task.TrackFile,
                    AlreadyExisted = alreadyRunning
                };
            }
            finally
            {
                tasks.TryRemove(task.Name, out _);
            }
        }

        private ConvTask GetConvTask(Track track, MCloudConfig.Converter conv)
        {
            var taskKey = track.id + "-" + conv.Name;
            var task = tasks.GetOrAdd(taskKey, (key) =>
            {
                return new ConvTask(this, track, conv, taskKey);
            });
            return task;
        }

        public struct ConvResult
        {
            public TrackFile TrackFile;
            public bool AlreadyExisted;
        }

        class ConvTask
        {

            public string Name { get; }
            public Track Track { get; }
            public MCloudConfig.Converter Conv { get; }

            private readonly ConvertService service;
            private readonly AppService app;
            private readonly ILogger<ConvertService> logger;
            private readonly IServiceProvider serviceProvider;

            public TrackFile TrackFile { private set; get; }
            public string OutputUrl { private set; get; }

            Task runningTask;

            public ConvTask(ConvertService service, Track track, MCloudConfig.Converter conv, string name)
            {
                this.service = service;
                this.serviceProvider = service.serviceProvider;
                this.logger = service.logger;
                this.app = service.serviceProvider.GetService<AppService>();
                Track = track;
                Conv = conv;
                Name = name;
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

            private async Task RunCore()
            {
                var url = Track.url;
                var outputUrl = Track.ConvUrl(Conv.Name);
                OutputUrl = outputUrl;

                var inputPath = app.Config.ResolveStoragePath(url);
                var outputPath = app.Config.ResolveStoragePath(outputUrl);

                string convCmdline = Conv.GetCommandLine(inputPath, outputPath);

                var debug = app.Config.ConverterDebug;

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

                if (app.StorageService.Mode != StorageMode.Direct)
                {
                    logger.LogInformation("'{id}-{conv}' uploading to storage service...", Track.id, Conv.Name);
                    await app.StorageService.PutFile(new RequestUploadOptions
                    {
                        DestFilePath = app.Config.GetStoragePath(outputUrl),
                        Length = trackFile.Size
                    }, outputPath);
                }

                using (var scope = serviceProvider.CreateScope())
                {
                    var dbctx = scope.ServiceProvider.GetService<DbCtx>();
                    dbctx.Attach(Track);
                RETRY:
                    // dbctx.Files.Add(task.TrackFile.File);
                    // dbctx.TrackFiles.Add(task.TrackFile);
                    Track.files.Add(TrackFile);
                    if (await dbctx.FailedSavingChanges())
                    {
                        await dbctx.Entry(Track).ReloadAsync();
                        goto RETRY;
                    }
                }
            }
        }
    }
}
