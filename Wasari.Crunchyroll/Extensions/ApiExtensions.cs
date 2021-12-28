using System.Collections.Generic;
using System.Linq;
using Wasari.Crunchyroll.Abstractions;
using Wasari.Crunchyroll.API;

namespace Wasari.Crunchyroll.Extensions;

internal static class ApiExtensions
{
    public static IEnumerable<CrunchyrollSeasonsInfo> ToSeasonsInfo(this IEnumerable<ApiSeason> apiSeasons,
        IEnumerable<ApiEpisode> apiEpisodes)
    {
        var episodeBySeason = apiEpisodes.ToLookup(i => i.SeasonId);

        var lastNumber = -1;
        foreach (var season in apiSeasons.OrderBy(i => i.Number))
        {
            if (lastNumber < 0)
            {
                lastNumber = season.Number;
            }

            var seasonInfo = new CrunchyrollSeasonsInfo
            {
                Id = season.Id,
                Season = episodeBySeason[season.Id].All(i => !i.EpisodeNumber.HasValue) ? 0 : lastNumber,
                Title = season.Title,
                Episodes = new List<CrunchyrollEpisodeInfo>()
            };

            foreach (var apiEpisode in episodeBySeason[season.Id])
            {
                seasonInfo.Episodes.Add(new CrunchyrollEpisodeInfo
                {
                    Id = apiEpisode.Id,
                    Name = apiEpisode.Title,
                    Special = !apiEpisode.EpisodeNumber.HasValue,
                    Url = apiEpisode.StreamLink,
                    ThumbnailId = null,
                    Number = (apiEpisode.EpisodeNumber ?? apiEpisode.SequenceNumber).ToString("00"),
                    SequenceNumber = apiEpisode.EpisodeNumber ?? apiEpisode.SequenceNumber,
                    SeasonInfo = seasonInfo,
                    Premium = apiEpisode.IsPremium
                });
            }

            if (seasonInfo.Season > 0)
                lastNumber++;
                
            yield return seasonInfo;
        }
    }
}