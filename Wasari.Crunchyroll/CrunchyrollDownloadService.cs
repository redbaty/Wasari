using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FFMpegCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wasari.App;
using Wasari.App.Abstractions;
using Wasari.FFmpeg;
using Wasari.Tvdb.Api.Client;
using Wasari.YoutubeDlp;

namespace Wasari.Crunchyroll;

internal static class EpisodeExtensions
{
    public static async IAsyncEnumerable<ApiEpisode> EnrichWithWasariApi(this IAsyncEnumerable<ApiEpisode> episodes, IServiceProvider serviceProvider, IOptions<DownloadOptions> downloadOptions)
    {
        var wasariTvdbApi = downloadOptions.Value.TryEnrichEpisodes ? serviceProvider.GetService<IWasariTvdbApi>() : null;

        if (wasariTvdbApi != null)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<IWasariTvdbApi>>();
            logger.LogInformation("Trying to enrich episodes with Wasari.Tvdb");

            var episodesArray = await episodes.ToArrayAsync();

            var seriesName = episodesArray.Select(o => o.SeriesTitle)
                .Distinct()
                .ToArray();

            if (seriesName.Length == 1)
            {
                var wasariApiEpisodes = await wasariTvdbApi.GetEpisodesAsync(seriesName.Single())
                    .ContinueWith(t =>
                    {
                        if (t.IsCompletedSuccessfully)
                        {
                            return t.Result;
                        }

                        logger.LogError(t.Exception, "Error while getting episodes from Wasari.Tvdb");
                        return null;
                    });

                if (wasariApiEpisodes != null)
                {
                    var episodesLookup = wasariApiEpisodes
                        .Where(i => !i.IsMovie)
                        .ToLookup(i => i.Name);

                    foreach (var episode in episodesArray)
                    {
                        var wasariEpisode = episodesLookup[episode.Title].SingleOrDefault();

                        if (wasariEpisode == null)
                        {
                            wasariEpisode = wasariApiEpisodes
                                .Where(i => !i.IsMovie)
                                .SingleOrDefault(o => o.Name.StartsWith(episode.Title, StringComparison.InvariantCultureIgnoreCase));

                            if (wasariEpisode == null)
                            {
                                logger.LogWarning("Skipping episode {EpisodeTitle} because it could not be found in Wasari.Tvdb", episode.Title);
                            }
                        }

                        if (wasariEpisode != null)
                            yield return episode with
                            {
                                SeasonNumber = wasariEpisode?.SeasonNumber ?? episode.SeasonNumber,
                                EpisodeNumber = wasariEpisode?.Number ?? episode.EpisodeNumber
                            };
                    }

                    yield break;
                }
            }

            foreach (var episode in episodesArray)
            {
                yield return episode;
            }

            yield break;
        }


        await foreach (var episode in episodes)
        {
            yield return episode;
        }
    }
}

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

                    var nonDubbedEpisode = await groupedEpisodes.SingleAsync(o => !o.IsDubbed);

                    return new WasariEpisode(nonDubbedEpisode.Title, nonDubbedEpisode.SeriesTitle, groupedEpisodes.Key.SeasonNumber, groupedEpisodes.Key.EpisodeNumber!.Value, null, async (provider) =>
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
                    }, TimeSpan.FromMilliseconds(nonDubbedEpisode.DurationMs));
                });

            return await base.DownloadEpisodes(episodes, levelOfParallelism);
        }

        return await base.DownloadEpisodes(url, levelOfParallelism);
    }

    private async IAsyncEnumerable<IWasariEpisodeInput> ProcessEpisode(ApiEpisode episode, CrunchyrollApiService crunchyrollApiService)
    {
        await episode.LoadStreams(crunchyrollApiService);

        if (episode.ApiEpisodeStreams?.Streams == null || episode.ApiEpisodeStreams.Streams.Length <= 0)
        {
            Logger.LogWarning("Episode found with no stream options: {@Episode}", episode);
            yield break;
        }

        var stream = episode.ApiEpisodeStreams.Streams.Single(o => o.Type == "adaptive_hls" && string.IsNullOrEmpty(o.Locale));
        var mediaInfo = await FFProbe.AnalyseAsync(new Uri(stream.Url));
        var bestVideo = mediaInfo.VideoStreams.OrderBy(vStream => vStream.Height + vStream.Width).Last();

        yield return new WasariEpisodeInputWithStream(stream.Url, episode.Locale, !episode.IsDubbed ? InputType.VideoWithAudio : InputType.Audio, mediaInfo.PrimaryAudioStream?.Index, !episode.IsDubbed ? bestVideo.Index : null);
    }
}