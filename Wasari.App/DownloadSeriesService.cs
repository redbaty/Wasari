using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Wasari.Abstractions;
using Wasari.Abstractions.Extensions;
using Wasari.App.Exceptions;
using Wasari.Crunchyroll;
using Wasari.Crunchyroll.Abstractions;
using Wasari.Crunchyroll.API;
using Wasari.Crunchyroll.Extensions;
using Wasari.Ffmpeg;
using Wasari.YoutubeDl;

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

    private static int[] ParseRange(string range, int max)
    {
        if (string.IsNullOrEmpty(range))
            return new[] { 0, max };

        if (range.Any(i => !char.IsDigit(i) && i != '-'))
            throw new InvalidEpisodeRangeException();

        if (range.Contains('-'))
        {
            var episodesNumbers = range.Split('-');

            if (episodesNumbers.Length != 2 || episodesNumbers.All(string.IsNullOrEmpty))
                throw new InvalidEpisodeRangeException();

            if (episodesNumbers.All(i => !string.IsNullOrEmpty(i)))
                return episodesNumbers.Select(int.Parse).ToArray();

            if (string.IsNullOrEmpty(episodesNumbers[0]))
                return new[] { 0, int.Parse(episodesNumbers[1]) };

            if (string.IsNullOrEmpty(episodesNumbers[1]))
                return new[] { int.Parse(episodesNumbers[0]), max };
        }

        if (int.TryParse(range, out var episode)) return new[] { episode, episode };

        throw new InvalidOperationException($"Invalid episode range. {range}");
    }

    public async Task DownloadEpisodes(Uri url, DownloadParameters downloadParameters)
    {
        var seriesProviderType = SeriesProviderSolver.GetProvider(url);

        if (ServiceProvider.GetService(seriesProviderType) is not ISeriesProvider seriesProvider)
            throw new InvalidOperationException($"Failed to create series provider. Type: {seriesProviderType.Name}");

        var episodes = await seriesProvider.GetEpisodes(url.ToString())
            .Where(i => !i.SeasonInfo.Dubbed || downloadParameters.Dubs && (downloadParameters.DubsLanguage == null || downloadParameters.DubsLanguage.Any(o => i.DubbedLanguage != null && i.DubbedLanguage.Contains(o, StringComparison.InvariantCultureIgnoreCase))))
            .OrderBy(i => i.SeasonInfo.Season)
            .GetEpisodesGrouped()
            .Reverse()
            .ToListAsync();

        if (!string.IsNullOrEmpty(downloadParameters.SeasonRange))
        {
            var seasonsRange = ParseRange(downloadParameters.SeasonRange, episodes.Select(i => i.SeasonInfo.Season).Max());
            episodes = episodes.Where(i =>
                    i.SeasonInfo.Season >= seasonsRange[0]
                    && i.SeasonInfo.Season <= seasonsRange[1])
                .ToList();
        }

        if (!string.IsNullOrEmpty(downloadParameters.EpisodeRange))
        {
            var episodeRange = ParseRange(downloadParameters.EpisodeRange, (int)episodes.Select(i => i.SequenceNumber).Max());
            Logger.LogInformation("Episodes range is {@Range}", episodeRange);
            episodes = episodes.Where(i =>
                    i.SequenceNumber >= episodeRange[0]
                    && i.SequenceNumber <= episodeRange[1])
                .ToList();
        }

        if (episodes.OfType<CrunchyrollEpisodeInfo>().Any(i => i.Premium) && !CrunchyrollApiServiceFactory.IsAuthenticated && downloadParameters.CookieFilePath == null)
            throw new PremiumEpisodesException(episodes.OfType<CrunchyrollEpisodeInfo>().Where(i => i.Premium).Cast<IEpisodeInfo>().ToArray());

        var series = episodes.Select(i => i.SeriesInfo).Distinct().Single();
        var outputDirectory = new DirectoryInfo( downloadParameters.FinalOutputDirectory(series.Name));

        if (!outputDirectory.Exists)
            outputDirectory.Create();

        if (downloadParameters.SkipExistingEpisodes)
        {
            foreach (var file in Directory.GetFiles(outputDirectory.FullName, "*.*", SearchOption.AllDirectories))
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

        if (!episodes.Any())
        {
            return;
        }

        var youtubeDlQueue = YoutubeDlQueueFactoryService.CreateQueue(episodes, downloadParameters, downloadParameters.DownloadPoolSize, true);
        var ytdlpTask = youtubeDlQueue.Start();
        var pool = new TaskPool<Task>(downloadParameters.EncodingPoolSize);

        if (youtubeDlQueue.ByEpisodeReader != null)
            await foreach (var youtubeDlResult in youtubeDlQueue.ByEpisodeReader.ReadAllAsync())
            {
                if (youtubeDlResult.Episode == null)
                    throw new InvalidOperationException("YoutubeDl episode is null");
                
                var finalEpisodeFile = youtubeDlResult.Episode.FinalEpisodeFile(downloadParameters);

                var episdeOutputDirectory = new DirectoryInfo(Path.GetDirectoryName(finalEpisodeFile) ??
                                                        throw new InvalidOperationException(
                                                            "Invalid output directory"));

                if (!episdeOutputDirectory.Exists)
                    episdeOutputDirectory.Create();

                DownloadedFile[]? additionalSubs = null;

                if (seriesProvider is BetaCrunchyrollService betaCrunchyrollService)
                {
                    if (youtubeDlResult.Results != null)
                    {
                        var subEpisodeId = youtubeDlResult.Results
                            .Where(i => !(i.Source?.Episode?.Dubbed ?? true))
                            .Select(i => i.Source?.Episode?.Id)
                            .FirstOrDefault(o => o != null);

                        additionalSubs = await betaCrunchyrollService.DownloadSubs(subEpisodeId, downloadParameters).ToArrayAsync();
                    }
                }

                await pool.Add(() => FfmpegService.Encode(youtubeDlResult.ToFfmpeg(additionalSubs), finalEpisodeFile, downloadParameters).ContinueWith(t =>
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
        await pool.WaitAndClose();
    }
}