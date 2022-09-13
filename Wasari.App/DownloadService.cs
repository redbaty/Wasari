using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TomLonghurst.EnumerableAsyncProcessor.Builders;
using Wasari.App.Abstractions;
using Wasari.FFmpeg;
using Wasari.YoutubeDlp;

namespace Wasari.App;

public class DownloadService
{
    public DownloadService(ILogger<DownloadService> logger, FFmpegService fFmpegService, IOptions<DownloadOptions> options, YoutubeDlpService youtubeDlpService, IServiceProvider serviceProvider)
    {
        Logger = logger;
        FFmpegService = fFmpegService;
        Options = options;
        YoutubeDlpService = youtubeDlpService;
        ServiceProvider = serviceProvider;
    }

    private ILogger<DownloadService> Logger { get; }

    private IOptions<DownloadOptions> Options { get; }

    private IServiceProvider ServiceProvider { get; }

    private FFmpegService FFmpegService { get; }

    private YoutubeDlpService YoutubeDlpService { get; }

    public Task<DownloadedEpisode[]> DownloadEpisodes(string url, int levelOfParallelism) => DownloadEpisodes(YoutubeDlpService.GetPlaylist(url)
        .OrderBy(i => i.SeasonNumber)
        .ThenBy(i => i.Number), levelOfParallelism);

    private async Task<DownloadedEpisode[]> DownloadEpisodes(IAsyncEnumerable<YoutubeDlEpisode> episodes, int levelOfParallelism)
    {
        var episodesArray = await episodes.GroupBy(i => i.ExtractorKey)
            .SelectMany(i =>
            {
                if (!Options.Value.Modifiers.TryGetValue(i.Key, out var modifierType)) return i;

                var modifierService = (IDownloadModifier)ServiceProvider.GetRequiredService(modifierType);
                return modifierService.Modify(i);
            })
            .FilterEpisodes(Options.Value.EpisodesRange, Options.Value.SeasonsRange)
            .ToArrayAsync();
        
        Logger.LogInformation("{@DownloadCount} episodes gathered to download", episodesArray.Length);

        return await AsyncProcessorBuilder.WithItems(episodesArray)
            .SelectAsync(DownloadEpisode)
            .ProcessInParallel(levelOfParallelism);
    }

    private async Task<DownloadedEpisode> DownloadEpisode(YoutubeDlEpisode episode)
    {
        var subtitleInputs = Options.Value.IncludeSubs
            ? episode.Subtitles
                .SelectMany(i => i.Value
                    .Select(o => new WasariEpisodeInput(o.Url, i.Key, InputType.Subtitle)))
            : ArraySegment<WasariEpisodeInput>.Empty;

        var inputs = episode.RequestedDownloads
            .Select(i => new WasariEpisodeInput(i.Url, i.Language, string.IsNullOrEmpty(i.Vcodec) ? InputType.Audio : InputType.Video))
            .Concat(subtitleInputs)
            .Cast<IWasariEpisodeInput>()
            .ToArray();

        var outputDirectory = Options.Value.OutputDirectory ?? Environment.CurrentDirectory;

        if (Options.Value.CreateSeriesFolder)
        {
            outputDirectory = Path.Combine(outputDirectory, episode.SeriesName);

            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);
        }
        
        if (Options.Value.CreateSeasonFolder)
        {
            outputDirectory = Path.Combine(outputDirectory, $"Season {episode.SeasonNumber}");

            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);
        }
        
        var episodeName = $"S{episode.SeasonNumber:00}E{episode.Number:00}";
        var fileName = $"{episodeName} - {episode.Title}.mkv".AsSafePath();
        var filepath = Path.Combine(outputDirectory, fileName);
        var wasariEpisode = new WasariEpisode(episode.Title, episode.SeasonNumber, episode.AbsoluteNumber, inputs, TimeSpan.FromSeconds(episode.Duration));

        if (Options.Value.SkipExistingFiles && File.Exists(filepath))
        {
            Logger.LogWarning("Skipping episode since it already exists: {Path}", filepath);
            return new DownloadedEpisode(filepath, false, wasariEpisode);
        }

        var episodeProgress = new Progress<double>();
        var lastValue = double.MinValue;

        episodeProgress.ProgressChanged += (_, d) =>
        {
            var delta = d - lastValue;

            if (delta > 0.01)
            {
                Logger.LogInformation("Encoding update {@Episode} {Path} {Percentage:p}", episodeName, filepath, d);
                lastValue = d;
            }
        };


        await FFmpegService.DownloadEpisode(wasariEpisode, filepath, episodeProgress);
        return new DownloadedEpisode(filepath, true, wasariEpisode);
    }
}