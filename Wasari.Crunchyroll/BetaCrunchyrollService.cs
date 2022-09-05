using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using TomLonghurst.EnumerableAsyncProcessor.Extensions;
using Wasari.Abstractions;
using Wasari.Crunchyroll.Abstractions;
using Wasari.Crunchyroll.API;
using Wasari.Crunchyroll.Extensions;

namespace Wasari.Crunchyroll;

public class BetaCrunchyrollService : ISeriesProvider
{
    public BetaCrunchyrollService(CrunchyrollApiServiceFactory crunchyrollApiServiceFactory)
    {
        CrunchyrollApiServiceFactory = crunchyrollApiServiceFactory;
    }

    private CrunchyrollApiServiceFactory CrunchyrollApiServiceFactory { get; }
    
    public async IAsyncEnumerable<DownloadedFile> DownloadSubs(string episodeId, DownloadParameters downloadParameters)
    {
        var crunchyService = CrunchyrollApiServiceFactory.GetService();
        var episode = await crunchyService.GetEpisode(episodeId);
        var streams = episode.ApiEpisodeStreams ?? await crunchyService.GetStreams(episode.StreamLink);
        var subtitles = streams.Subtitles;
        
        using var httpClient = new HttpClient();

        foreach (var subtitle in subtitles)
        {
            using var respostaHttp = await httpClient.GetAsync(subtitle.Url);
            await using var remoteStream = await respostaHttp.Content.ReadAsStreamAsync();
            var temporaryFile = Path.Combine(downloadParameters.TemporaryDirectory ?? Path.GetTempPath(), $"{episodeId}.{subtitle.Locale}.{subtitle.Format}");
            var directory = Path.GetDirectoryName(temporaryFile);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            await using var fs = File.Create(temporaryFile);
            await remoteStream.CopyToAsync(fs);

            yield return new SubtitleFile
            {
                Type = FileType.Subtitle,
                Path = temporaryFile,
                Language = subtitle.Locale.Replace("-", string.Empty).ToLower()
            };
        }
    }

    public async IAsyncEnumerable<IEpisodeInfo> GetEpisodes(string url)
    {
        var crunchyService = CrunchyrollApiServiceFactory.GetService();
        var match = Regex.Match(url, @"series\/(?<seriesId>\w+)|watch\/(?<episodeId>\w+)\/");

        
        var seriesId = match.Groups["seriesId"].Value;

        if (match.Groups["episodeId"].Success)
        {
            var episode = await crunchyService.GetEpisode(match.Groups["episodeId"].Value);
            var season = await crunchyService.GetSeason(episode.SeasonId);
            seriesId = season.SeriesId;

            var seriesInfo = await crunchyService.GetSeriesInformation(seriesId);
            var crunchyrollSeriesInfo = new CrunchyrollSeriesInfo
            {
                Id = seriesId,
                Name = seriesInfo.Title
            };
            
            foreach (var episodeInfo in new[] { season }.ToSeasonsInfo(new[] { episode }, crunchyrollSeriesInfo).SelectMany(i => i.Episodes))
            {
                yield return episodeInfo;
            }
        }

        if (!match.Groups["seriesId"].Success)
        {
            throw new Exception("Failed to determined series ID from URL");
        }

        var info = await crunchyService.GetSeriesInformation(seriesId);

        var seasons = await crunchyService
            .GetSeasons(seriesId)
            .ToArrayAsync();

        var episodes = await seasons
            .ToAsyncProcessorBuilder()
            .SelectAsync(season => crunchyService.GetEpisodes(season.Id).ToArrayAsync().AsTask())
            .ProcessInParallel(3);

        var seasonsInfo = seasons.ToSeasonsInfo(episodes.SelectMany(o => o), new CrunchyrollSeriesInfo
        {
            Id = seriesId,
            Name = info.Title
        }).ToArray();
        
        foreach (var episodeInfo in seasonsInfo.SelectMany(o => o.Episodes))
        {
            yield return episodeInfo;
        }
    }
}