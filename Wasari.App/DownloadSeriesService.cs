using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Wasari.Abstractions;
using Wasari.Abstractions.Extensions;
using Wasari.Crunchyroll;
using Wasari.Crunchyroll.Extensions;
using Wasari.Ffmpeg;

namespace Wasari.App;

public class DownloadSeriesService
{
    public DownloadSeriesService(IServiceProvider serviceProvider, SeriesProviderSolver seriesProviderSolver, YoutubeDlQueueFactoryService youtubeDlQueueFactoryService, FfmpegService ffmpegService, ILogger<DownloadSeriesService> logger)
    {
        ServiceProvider = serviceProvider;
        SeriesProviderSolver = seriesProviderSolver;
        YoutubeDlQueueFactoryService = youtubeDlQueueFactoryService;
        FfmpegService = ffmpegService;
        Logger = logger;
    }

    private IServiceProvider ServiceProvider { get; }

    private SeriesProviderSolver SeriesProviderSolver { get; }

    private YoutubeDlQueueFactoryService YoutubeDlQueueFactoryService { get; }

    private FfmpegService FfmpegService { get; }
    
    private ILogger<DownloadSeriesService> Logger { get; }


    public async Task DownloadEpisodes(Uri url, DownloadParameters downloadParameters)
    {
        var seriesProviderType = SeriesProviderSolver.GetProvider(url);

        if (ServiceProvider.GetService(seriesProviderType) is not ISeriesProvider seriesProvider)
            throw new InvalidOperationException($"Failed to create series provider. Type: {seriesProviderType.Name}");
        
        var episodes = await seriesProvider.GetEpisodes(url.ToString())
            .Where(i => !i.SeasonInfo.Dubbed || downloadParameters.Dubs && (downloadParameters.DubsLanguage == null || downloadParameters.DubsLanguage.Any(o => i.DubbedLanguage != null && i.DubbedLanguage.Contains(o))))
            .OrderBy(i => i.SeasonInfo.Season)
            .GetEpisodesGrouped()
            .Reverse()
            .ToListAsync();
        
        if (downloadParameters.SkipExistingEpisodes)
        {
            var series = episodes.Select(i => i.SeriesInfo).Distinct().Single();
            var outputDirectory = downloadParameters.FinalOutputDirectory(series.Name);
            
            foreach (var file in Directory.GetFiles(outputDirectory, "*.*", SearchOption.AllDirectories))
            {
                const string regex = @"S(?<season>\d+)E(?<episode>\d+) -";
                
                var episodeMatch = Regex.Match(file, regex);

                if (!episodeMatch.Success
                    || !int.TryParse(episodeMatch.Groups["episode"].Value, out var episode)
                    || !int.TryParse(episodeMatch.Groups["season"].Value, out var season)) continue;
                
                
                var removed = episodes.RemoveAll(i => i.SeasonInfo.Season == season && i.SequenceNumber == episode);

                if (removed > 0)
                {
                    Logger.LogWarning(
                        "Skipping episode {@EpisodeNumber} from season {@SeasonNumber} due to existing file {@FilePath}",
                        episode, season, file);
                }
            }
        }
        
        var youtubeDlQueue = YoutubeDlQueueFactoryService.CreateQueue(episodes, downloadParameters, downloadParameters.DownloadPoolSize, true);
        var ytdlpTask = youtubeDlQueue.Start();
        var pool = new TaskPool<Task>(downloadParameters.EncodingPoolSize);

        await foreach (var youtubeDlResult in youtubeDlQueue.ByEpisodeReader.ReadAllAsync())
        {
            var finalEpisodeFile = youtubeDlResult.Episode.FinalEpisodeFile(downloadParameters);

            var outputDirectory = new DirectoryInfo(Path.GetDirectoryName(finalEpisodeFile) ??
                                                    throw new InvalidOperationException(
                                                        "Invalid output directory"));

            if (!outputDirectory.Exists)
                outputDirectory.Create();

            await pool.Add(() => FfmpegService.Encode(youtubeDlResult, finalEpisodeFile, downloadParameters).ContinueWith(t =>
            {
                Logger.LogProgressUpdate(new ProgressUpdate
                {
                    Type = ProgressUpdateTypes.Completed,
                    EpisodeId = youtubeDlResult.Episode?.Id,
                    Title = $"[DONE] {Path.GetFileName(finalEpisodeFile)}"
                });

                if (!t.IsCompletedSuccessfully)
                {
                    Logger.LogError(t.Exception, "Failed while running encoding for episode {@Id}", youtubeDlResult.Episode?.FilePrefix);
                    throw t.Exception;
                }
            }));
        }

        await ytdlpTask;
        await pool.WaitToReachEnqueuedCount();
    }
}