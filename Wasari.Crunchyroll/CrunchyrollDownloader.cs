using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using Wasari.Abstractions;
using Wasari.Abstractions.Extensions;
using Wasari.Crunchyroll.Abstractions;

namespace Wasari.Crunchyroll
{
    internal class CrunchyrollDownloader : ISeriesDownloader<CrunchyrollEpisodeInfo>
    {
        public CrunchyrollDownloader(YoutubeDlQueueService youtubeDlQueueService, FfmpegQueueService ffmpegQueueService, ILogger<CrunchyrollDownloader> logger)
        {
            YoutubeDlQueueService = youtubeDlQueueService;
            FfmpegQueueService = ffmpegQueueService;
            Logger = logger;
        }

        private YoutubeDlQueueService YoutubeDlQueueService { get; }
        
        private FfmpegQueueService FfmpegQueueService { get; }
        
        private ILogger<CrunchyrollDownloader> Logger { get; }

        public async IAsyncEnumerable<DownloadedFile> DownloadEpisodes(IEnumerable<CrunchyrollEpisodeInfo> episodes, DownloadParameters downloadParameters)
        {
            var ytDlTask = YoutubeDlQueueService.Start(episodes, downloadParameters, downloadParameters.ParallelDownloads);
            var ffmpegTask = FfmpegQueueService.Start(downloadParameters, downloadParameters.ParallelMerging);
                
            await foreach (var youtubeDlResult in YoutubeDlQueueService.Reader.ReadAllAsync())
            {
                yield return new DownloadedFile
                {
                    Path = youtubeDlResult.FinalEpisodeFile(downloadParameters),
                    Type = FileType.VideoFile
                };
                
                if (downloadParameters.Subtitles || downloadParameters.UseHevc)
                {
                    await FfmpegQueueService.Enqueue(youtubeDlResult);
                }
                else
                {
                    var finalEpisodeFile = youtubeDlResult.FinalEpisodeFile(downloadParameters);
                    File.Move(youtubeDlResult.TemporaryEpisodeFile.Path, finalEpisodeFile);

                    Logger.LogProgressUpdate(new ProgressUpdate
                    {
                        Type = ProgressUpdateTypes.Completed,
                        EpisodeId = youtubeDlResult.Episode.FilePrefix,
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