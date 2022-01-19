using System;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Wasari.Abstractions;
using Wasari.Crunchyroll;
using Wasari.Crunchyroll.Abstractions;
using Wasari.Crunchyroll.API;

namespace Wasari.Commands;

[Command("crunchy-list")]
internal class CrunchyrollListSeriesCommand : ICommand
{
    [CommandParameter(0, Description = "Series URL.")]
    public string SeriesUrl { get; init; }
        
    public CrunchyrollListSeriesCommand(ISeriesProvider<CrunchyrollSeasonsInfo> crunchyrollSeasonProvider, CrunchyrollApiServiceFactory crunchyrollApiServiceFactory, BetaCrunchyrollService betaCrunchyrollService)
    {
        CrunchyrollSeasonProvider = crunchyrollSeasonProvider;
        CrunchyrollApiServiceFactory = crunchyrollApiServiceFactory;
        BetaCrunchyrollService = betaCrunchyrollService;
    }
    
    private CrunchyrollApiServiceFactory CrunchyrollApiServiceFactory { get; }
    
    private BetaCrunchyrollService BetaCrunchyrollService { get; }

    private ISeriesProvider<CrunchyrollSeasonsInfo> CrunchyrollSeasonProvider { get; }
        
    public async ValueTask ExecuteAsync(IConsole console)
    {
        var isBeta = SeriesUrl.Contains("beta.");
    
        await CrunchyrollApiServiceFactory.CreateUnauthenticatedService();

        var seriesInfo = isBeta
            ? await BetaCrunchyrollService.GetSeries(SeriesUrl)
            : await CrunchyrollSeasonProvider.GetSeries(SeriesUrl);

        foreach (var seasonsInfo in seriesInfo.Seasons)
        {
            await console.Output.WriteLineAsync($"S{seasonsInfo.Season:00} - {seasonsInfo.Title}");

            foreach (var episode in seasonsInfo.Episodes)
            {
                await console.Output.WriteLineAsync($"- {episode.Number}: {episode.Name}");
            }

            await console.Output.WriteLineAsync();
        }
    }
}