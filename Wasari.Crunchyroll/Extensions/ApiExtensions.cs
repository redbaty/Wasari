using System.Collections.Generic;
using System.Linq;
using Wasari.Abstractions;
using Wasari.Crunchyroll.Abstractions;
using Wasari.Crunchyroll.API;

namespace Wasari.Crunchyroll.Extensions;

internal static class ApiExtensions
{
    public static IEnumerable<CrunchyrollSeasonsInfo> ToSeasonsInfo(this IEnumerable<ApiSeason> apiSeasons,
        IEnumerable<ApiEpisode> apiEpisodes, ISeriesInfo seriesInfo)
    {
        var episodeBySeason = apiEpisodes.ToLookup(i => i.SeasonId);

        var lastNumber = 1;
        foreach (var season in apiSeasons.OrderBy(i => i.Number))
        {
            if (lastNumber < 0)
            {
                lastNumber = season.Number;
            }

            var seasonInfo = new CrunchyrollSeasonsInfo
            {
                Id = season.Id,
                Season = lastNumber,
                Title = season.Title,
                Dubbed = season.IsDubbed,
                DubbedLanguage = season.IsDubbed ? GetSeasonDubLanguage(season, episodeBySeason[season.Id]) : season.Title,
                Episodes = new List<IEpisodeInfo>()
            };

            foreach (var apiEpisode in episodeBySeason[season.Id])
            {
                var streams = apiEpisode.ApiEpisodeStreams?.Streams;

                if (streams is not { Length: > 0 })
                    continue;

                var episodeUrl = streams
                    .SingleOrDefault(i => i.Type == "adaptive_hls" && string.IsNullOrEmpty(i.Locale))?.Url ?? apiEpisode.StreamLink;
                var crunchyrollEpisodeInfo = new CrunchyrollEpisodeInfo
                {
                    Id = apiEpisode.Id,
                    Name = apiEpisode.Title,
                    Special = !apiEpisode.EpisodeNumber.HasValue || string.IsNullOrEmpty(apiEpisode.Episode),
                    Url = episodeUrl,
                    ThumbnailId = null,
                    Number = (apiEpisode.EpisodeNumber ?? apiEpisode.SequenceNumber).ToString("00"),
                    SequenceNumber = apiEpisode.EpisodeNumber ?? apiEpisode.SequenceNumber,
                    SeasonInfo = seasonInfo,
                    Premium = apiEpisode.IsPremium,
                    Dubbed = apiEpisode.IsDubbed,
                    DubbedLanguage = apiEpisode.AudioLocale,
                    SeriesInfo = seriesInfo
                };

                crunchyrollEpisodeInfo.Sources.Add(new EpisodeInfoVideoSource
                {
                    Url = episodeUrl,
                    Language = seasonInfo.DubbedLanguage,
                    Episode = crunchyrollEpisodeInfo
                });

                seasonInfo.Episodes.Add(crunchyrollEpisodeInfo);
            }

            if (seasonInfo.Season > 0 && !season.IsDubbed)
                lastNumber++;

            yield return seasonInfo;
        }
    }

    private static string GetSeasonDubLanguage(ApiSeason season, IEnumerable<ApiEpisode> apiEpisodes)
    {
        if (season.AudioLocales is { Length: 1 })
        {
            return season.AudioLocales.Single();
        }
        
        var subLanguages = apiEpisodes.Select(o =>
            {
                if (string.IsNullOrEmpty(o.AudioLocale) && o.Subtitles is { Length: 1 })
                    return o.Subtitles.Single();

                return o.AudioLocale;
            }).Distinct()
            .Where(i => !string.IsNullOrEmpty(i))
            .ToArray();
        
        if (subLanguages.Length == 1)
            return subLanguages.Single();

        throw new System.NotImplementedException();
    }
}