using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Wasari.Abstractions;
using Wasari.Crunchyroll.Abstractions;
using Wasari.Crunchyroll.API;
using Wasari.Crunchyroll.Extensions;

namespace Wasari.Crunchyroll;

public class BetaCrunchyrollService : ISeriesProvider<CrunchyrollSeasonsInfo>
{
    public BetaCrunchyrollService(CrunchyrollApiServiceFactory crunchyrollApiServiceFactory)
    {
        CrunchyrollApiServiceFactory = crunchyrollApiServiceFactory;
    }

    private CrunchyrollApiServiceFactory CrunchyrollApiServiceFactory { get; }

    public async Task<ISeriesInfo<CrunchyrollSeasonsInfo>> GetSeries(string url)
    {
        var match = Regex.Match(url, @"series\/(\w+)\/");
        var id = match.Groups[1].Value;
        var crunchyService = CrunchyrollApiServiceFactory.GetService();
        var info = await crunchyService.GetSeriesInformation(id);

        var seasons = await crunchyService
            .GetSeasons(id)
            .Where(i => !i.IsDubbed && i.IsSubbed)
            .ToArrayAsync();

        var episodes = await seasons
            .ToAsyncEnumerable()
            .SelectMany(i => crunchyService.GetEpisodes(i.Id))
            .ToArrayAsync();

        var seasonsInfo = seasons.ToSeasonsInfo(episodes).ToArray();

        return new CrunchyrollSeriesInfo
        {
            Id = id,
            Name = info.Title,
            Seasons = seasonsInfo
        };
    }
}