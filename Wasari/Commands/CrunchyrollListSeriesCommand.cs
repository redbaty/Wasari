using System;
using System.Linq;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Wasari.Abstractions;
using Wasari.Crunchyroll;
using Wasari.Crunchyroll.API;

namespace Wasari.Commands;

[Command("crunchy-list")]
internal class CrunchyrollListSeriesCommand : ICommand
{
    [CommandParameter(0, Description = "Series URL.")]
    public string SeriesUrl { get; init; }
        
    public CrunchyrollListSeriesCommand(ISeriesProvider crunchyrollSeasonProvider, CrunchyrollApiServiceFactory crunchyrollApiServiceFactory, BetaCrunchyrollService betaCrunchyrollService)
    {
        CrunchyrollSeasonProvider = crunchyrollSeasonProvider;
        CrunchyrollApiServiceFactory = crunchyrollApiServiceFactory;
        BetaCrunchyrollService = betaCrunchyrollService;
    }
    
    private CrunchyrollApiServiceFactory CrunchyrollApiServiceFactory { get; }
    
    private BetaCrunchyrollService BetaCrunchyrollService { get; }

    private ISeriesProvider CrunchyrollSeasonProvider { get; }
        
    public async ValueTask ExecuteAsync(IConsole console)
    {
        var isBeta = SeriesUrl.Contains("beta.");
    
        await CrunchyrollApiServiceFactory.CreateUnauthenticatedService();

        var episodes = isBeta
            ? await BetaCrunchyrollService.GetEpisodes(SeriesUrl).ToArrayAsync()
            : await CrunchyrollSeasonProvider.GetEpisodes(SeriesUrl).ToArrayAsync();
        
        foreach (var episodesBySeason in episodes.GroupBy(i => i.SeasonInfo))
        {
            await console.Output.WriteLineAsync($"S{episodesBySeason.Key.Season:00} - {episodesBySeason.Key.Title}");

            foreach (var episode in episodesBySeason)
            {
                await console.Output.WriteLineAsync($"- {episode.Number}: {episode.Name}");
            }

            await console.Output.WriteLineAsync();
        }
    }
}