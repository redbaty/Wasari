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
using Wasari.YoutubeDlp;

namespace Wasari.Crunchyroll;

internal class CrunchyrollDownloadService : GenericDownloadService
{
    private CrunchyrollApiService CrunchyrollApiService { get; }
    
    private IOptions<DownloadOptions> DownloadOptions { get; }

    public CrunchyrollDownloadService(ILogger<CrunchyrollDownloadService> logger, FFmpegService fFmpegService, IOptions<DownloadOptions> options, YoutubeDlpService youtubeDlpService, IServiceProvider serviceProvider, CrunchyrollApiService crunchyrollApiService, IOptions<DownloadOptions> downloadOptions) : base(logger, fFmpegService, options,
        youtubeDlpService)
    {
        CrunchyrollApiService = crunchyrollApiService;
        DownloadOptions = downloadOptions;
    }

    public override async Task<DownloadedEpisode[]> DownloadEpisodes(string url, int levelOfParallelism)
    {
        var match = Regex.Match(url, @"series\/(?<seriesId>\w+)|watch\/(?<episodeId>\w+)\/");

        if (match.Groups["seriesId"].Success)
        {
            var seriesId = match.Groups["seriesId"].Value;
            var episodes = CrunchyrollApiService.GetAllEpisodes(seriesId)
                .Where(i => i.EpisodeNumber.HasValue && (DownloadOptions.Value.IncludeDubs || !i.IsDubbed))
                .GroupBy(i => new { i.EpisodeNumber, i.SeasonNumber, i.SeriesTitle })
                .SelectAwait(async groupedEpisodes =>
                {
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

    private static async IAsyncEnumerable<IWasariEpisodeInput> ProcessEpisode(ApiEpisode i, CrunchyrollApiService crunchyrollApiService)
    {
        await i.LoadStreams(crunchyrollApiService);
        var stream = i.ApiEpisodeStreams.Streams.Single(o => o.Type == "adaptive_hls" && string.IsNullOrEmpty(o.Locale));
        var mediaInfo = await FFProbe.AnalyseAsync(new Uri(stream.Url));
        var bestVideo = mediaInfo.VideoStreams.OrderBy(vStream => vStream.Height + vStream.Width).Last();

        yield return new WasariEpisodeInputWithStream(stream.Url, i.Locale, !i.IsDubbed ? InputType.VideoWithAudio : InputType.Audio, mediaInfo.PrimaryAudioStream?.Index, !i.IsDubbed ? bestVideo.Index : null);
    }
}