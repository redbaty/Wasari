using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Wasari.Abstractions;
using Wasari.Abstractions.Extensions;
using Wasari.Crunchyroll.Abstractions;
using Wasari.Crunchyroll.Extensions;
using Wasari.Ffmpeg;
using Wasari.YoutubeDl;

namespace Wasari.Crunchyroll
{
    internal class CrunchyrollDownloader : ISeriesDownloader<CrunchyrollEpisodeInfo>
    {
        public CrunchyrollDownloader(YoutubeDlQueueFactoryService youtubeDlQueueFactoryService, FfmpegQueueService ffmpegQueueService, ILogger<CrunchyrollDownloader> logger)
        {
            YoutubeDlQueueFactoryService = youtubeDlQueueFactoryService;
            FfmpegQueueService = ffmpegQueueService;
            Logger = logger;
        }

        private YoutubeDlQueueFactoryService YoutubeDlQueueFactoryService { get; }
        
        private FfmpegQueueService FfmpegQueueService { get; }
        
        private ILogger<CrunchyrollDownloader> Logger { get; }

        public async IAsyncEnumerable<DownloadedFile> DownloadEpisodes(IEnumerable<CrunchyrollEpisodeInfo> episodes, DownloadParameters downloadParameters)
        {
            if (downloadParameters.TemporaryDirectory != null)
            {
                if (!Directory.Exists(downloadParameters.TemporaryDirectory))
                    Directory.CreateDirectory(downloadParameters.TemporaryDirectory);
            }

            var youtubeDlQueue = YoutubeDlQueueFactoryService.CreateQueue(episodes, downloadParameters, downloadParameters.ParallelDownloads);
            var ytDlTask = youtubeDlQueue.Start();
            var ffmpegTask = FfmpegQueueService.Start(downloadParameters, downloadParameters.ParallelMerging);
                
            await foreach (var youtubeDlResultByEpisode in youtubeDlQueue.ByEpisodeReader.ReadAllAsync())
            {
                var youtubeDlResult = youtubeDlResultByEpisode.Results.Single();

                if (youtubeDlResult.Episode == null)
                {
                    continue;
                }
                
                yield return new DownloadedFile
                {
                    Path = youtubeDlResult.Episode.FinalEpisodeFile(downloadParameters),
                    Type = FileType.VideoFile
                };
                
                if (downloadParameters.Subtitles || downloadParameters.UseHevc)
                {
                    await FfmpegQueueService.Enqueue(youtubeDlResultByEpisode.ToFfmpeg());
                }
                else
                {
                    var finalEpisodeFile = youtubeDlResult.Episode.FinalEpisodeFile(downloadParameters);
                    var finalEpisodeDirectory = Path.GetDirectoryName(finalEpisodeFile);

                    if (finalEpisodeDirectory != null && !Directory.Exists(finalEpisodeDirectory))
                        Directory.CreateDirectory(finalEpisodeDirectory);

                    if (string.IsNullOrEmpty(youtubeDlResult.TemporaryEpisodeFile.Path))
                        throw new InvalidOperationException("Invalid temporary file path");

                    File.Move(youtubeDlResult.TemporaryEpisodeFile.Path, finalEpisodeFile, true);

                    Logger.LogProgressUpdate(new ProgressUpdate
                    {
                        Type = ProgressUpdateTypes.Completed,
                        EpisodeId = youtubeDlResult.Episode.Id,
                        Title = $"[DONE] {Path.GetFileName(finalEpisodeFile)}"
                    });
                }
            }
                
            FfmpegQueueService.Ended();
            await ytDlTask;
            await ffmpegTask;
        }
    }
}