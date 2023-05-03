using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FFMpegCore;
using FFMpegCore.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wasari.App;
using Wasari.App.Abstractions;
using Wasari.FFmpeg;
using Wasari.YoutubeDlp;

namespace Wasari.Crunchyroll;

internal class CrunchyrollDownloadService : GenericDownloadService
{
    private CrunchyrollApiService CrunchyrollApiService { get; }

    private IOptions<DownloadOptions> DownloadOptions { get; }

    private IOptions<AuthenticationOptions> AuthenticationOptions { get; }

    private IServiceProvider ServiceProvider { get; }

    public CrunchyrollDownloadService(ILogger<CrunchyrollDownloadService> logger, FFmpegService fFmpegService, IOptions<DownloadOptions> options, YoutubeDlpService youtubeDlpService, CrunchyrollApiService crunchyrollApiService, IOptions<DownloadOptions> downloadOptions,
        IServiceProvider serviceProvider, IOptions<AuthenticationOptions> authenticationOptions) : base(logger, fFmpegService,
        options,
        youtubeDlpService)
    {
        CrunchyrollApiService = crunchyrollApiService;
        DownloadOptions = downloadOptions;
        ServiceProvider = serviceProvider;
        AuthenticationOptions = authenticationOptions;
    }

    public override async Task<DownloadedEpisode[]> DownloadEpisodes(string url, int levelOfParallelism)
    {
        var match = Regex.Match(url, @"series\/(?<seriesId>\w+)|watch\/(?<episodeId>\w+)\/");

        if (match.Groups["seriesId"].Success)
        {
            var seriesId = match.Groups["seriesId"].Value;

            var episodes = CrunchyrollApiService.GetAllEpisodes(seriesId)
                .Where(i => i.EpisodeNumber.HasValue && (DownloadOptions.Value.IncludeDubs || !i.IsDubbed))
                .EnrichWithWasariApi(ServiceProvider, DownloadOptions)
                .GroupBy(i => new { i.EpisodeNumber, i.SeasonNumber, i.SeriesTitle })
                .SelectAwait(async groupedEpisodes =>
                {
                    if (!AuthenticationOptions.Value.HasCredentials && await groupedEpisodes.AnyAsync(o => o.IsPremium))
                        throw new PremiumEpisodeException(groupedEpisodes.Key.EpisodeNumber!.Value, groupedEpisodes.Key.SeasonNumber);

                    var hasNonDubbedEpisodes = await groupedEpisodes.AnyAsync(i => !i.IsDubbed);

                    var commonEpisodeData = hasNonDubbedEpisodes
                        ? await groupedEpisodes
                            .Where(i => !i.IsDubbed)
                            .Select(i => new
                            {
                                i.SeriesTitle,
                                i.Title,
                                i.DurationMs
                            })
                            .Distinct()
                            .SingleAsync()
                        : await groupedEpisodes
                            .GroupBy(i => new { i.SeriesTitle, i.Title })
                            .SelectAwait(async i => new
                            {
                                i.Key.SeriesTitle,
                                i.Key.Title,
                                DurationMs = await i.MaxAsync(o => o.DurationMs)
                            })
                            .Distinct()
                            .SingleAsync();

                    return new WasariEpisode(commonEpisodeData.Title, commonEpisodeData.SeriesTitle, groupedEpisodes.Key.SeasonNumber, groupedEpisodes.Key.EpisodeNumber!.Value, null, async (provider) =>
                    {
                        var crunchyrollApiService = provider.GetRequiredService<CrunchyrollApiService>();

                        return await groupedEpisodes
                            .SelectMany(i => ProcessEpisode(i, crunchyrollApiService))
                            .Concat(groupedEpisodes
                                .Where(o => o?.ApiEpisodeStreams?.Subtitles is { Length: > 0 })
                                .SelectMany(o => o.ApiEpisodeStreams.Subtitles.ToAsyncEnumerable())
                                .GroupBy(i => i.Locale)
                                .SelectAwait(i => i.FirstAsync())
                                .Select(o => new WasariEpisodeInput(o.Url, o.Locale, InputType.Subtitle)))
                            .Cast<IWasariEpisodeInput>()
                            .ToArrayAsync();
                    }, TimeSpan.FromMilliseconds(commonEpisodeData.DurationMs));
                });

            return await base.DownloadEpisodes(episodes, levelOfParallelism);
        }

        return await base.DownloadEpisodes(url, levelOfParallelism);
    }

    private async IAsyncEnumerable<IWasariEpisodeInput> ProcessEpisode(ApiEpisode episode, CrunchyrollApiService crunchyrollApiService)
    {
        await episode.LoadStreams(crunchyrollApiService);

        if (episode.ApiEpisodeStreams?.Streams is not { Length: > 0 })
        {
            Logger.LogWarning("Episode found with no stream options: {@Episode}", episode);
            yield break;
        }

        var stream = episode.ApiEpisodeStreams.Streams.Single(o => o.Type == "adaptive_hls" && string.IsNullOrEmpty(o.Locale));
        var mediaInfo = await FFProbe.AnalyseAsync(new Uri(stream.Url), new FFOptions
        {
            LogLevel = FFMpegLogLevel.Error,
            UseCache = false
        });
        var bestVideo = mediaInfo.VideoStreams.OrderBy(vStream => vStream.Height + vStream.Width).Last();

        yield return new WasariEpisodeInputWithStream(stream.Url, episode.Locale, !episode.IsDubbed ? InputType.VideoWithAudio : InputType.Audio, mediaInfo.PrimaryAudioStream?.Index, !episode.IsDubbed ? bestVideo.Index : null);
    }
}