using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wasari.Abstractions;
using Wasari.Abstractions.Extensions;

namespace Wasari.Ffmpeg
{
    public class FfmpegQueueService
    {
        public FfmpegQueueService(FfmpegService ffmpegService, ILogger<FfmpegQueueService> logger)
        {
            FfmpegService = ffmpegService;
            Logger = logger;
        }

        private FfmpegService FfmpegService { get; }

        private Channel<FfmpegEpisodeToEncode> Channel { get; } = System.Threading.Channels.Channel.CreateUnbounded<FfmpegEpisodeToEncode>();

        private ILogger<FfmpegQueueService> Logger { get; }

        public ValueTask Enqueue(FfmpegEpisodeToEncode episodeToEncode)
        {
            return Channel.Writer.WriteAsync(episodeToEncode);
        }

        public void Ended()
        {
            Channel.Writer.Complete();
        }

        public async Task Start(DownloadParameters downloadParameters, int poolSize)
        {
            var tasks = new List<Task>(poolSize);

            await foreach (var episode in Channel.Reader.ReadAllAsync())
            {
                if (tasks.Count >= poolSize)
                {
                    var task = await Task.WhenAny(tasks);
                    tasks.Remove(task);
                }
                
                var episodeFile = episode.Episode.FinalEpisodeFile(downloadParameters);

                var outputDirectory = new DirectoryInfo(Path.GetDirectoryName(episodeFile) ??
                                                        throw new InvalidOperationException(
                                                            "Invalid output directory"));

                if (!outputDirectory.Exists)
                    outputDirectory.Create();

                tasks.Add(FfmpegService.Encode(episode, episodeFile, downloadParameters).ContinueWith(t =>
                {
                    Logger.LogProgressUpdate(new ProgressUpdate
                    {
                        Type = ProgressUpdateTypes.Completed,
                        EpisodeId = episode?.Episode.Id,
                        Title = $"[DONE] {Path.GetFileName(episodeFile)}"
                    });

                    if (!t.IsCompletedSuccessfully)
                    {
                        Logger.LogError(t.Exception, "Failed while running encoding for episode {@Id}", episode.Episode?.FilePrefix);
                    }
                }));
            }
            
            await Task.WhenAll(tasks);
        }
    }
}