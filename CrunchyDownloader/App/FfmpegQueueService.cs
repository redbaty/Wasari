using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;
using CrunchyDownloader.Models;
using Microsoft.Extensions.Logging;

namespace CrunchyDownloader.App
{
    internal class FfmpegQueueService
    {
        public FfmpegQueueService(FfmpegService ffmpegService, ILogger<FfmpegQueueService> logger)
        {
            FfmpegService = ffmpegService;
            Logger = logger;
        }

        private FfmpegService FfmpegService { get; }

        private Channel<YoutubeDlResult> Channel { get; } =
            System.Threading.Channels.Channel.CreateUnbounded<YoutubeDlResult>();

        private ILogger<FfmpegQueueService> Logger { get; }

        public ValueTask Enqueue(YoutubeDlResult youtubeDlResult)
        {
            return Channel.Writer.WriteAsync(youtubeDlResult);
        }

        public void Ended()
        {
            Channel.Writer.Complete();
        }

        public async Task Start(DownloadParameters downloadParameters, int poolSize)
        {
            var tasks = new List<Task>(poolSize);

            await foreach (var youtubeDlResult in Channel.Reader.ReadAllAsync())
            {
                if (tasks.Count >= poolSize)
                {
                    var task = await Task.WhenAny(tasks);
                    tasks.Remove(task);
                }


                var episodeFile = youtubeDlResult.FinalEpisodeFile(downloadParameters);

                var outputDirectory = new DirectoryInfo(Path.GetDirectoryName(episodeFile) ??
                                                        throw new InvalidOperationException(
                                                            "Invalid output directory"));

                if (!outputDirectory.Exists)
                    outputDirectory.Create();

                tasks.Add(FfmpegService.Encode(youtubeDlResult, episodeFile, downloadParameters).ContinueWith(t =>
                {
                    Logger.LogProgressUpdate(new ProgressUpdate
                    {
                        Type = ProgressUpdateTypes.Completed,
                        EpisodeId = youtubeDlResult.Episode?.Id,
                        Title = $"[DONE] {Path.GetFileName(episodeFile)}"
                    });

                    if (!t.IsCompletedSuccessfully)
                    {
                        Logger.LogError(t.Exception, "Failed while running encoding for episode {@Id}", youtubeDlResult.Episode?.Id);
                    }
                }));
            }
            
            await Task.WhenAll(tasks);
        }
    }
}