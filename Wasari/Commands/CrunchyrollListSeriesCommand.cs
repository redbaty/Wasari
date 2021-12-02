using System;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Wasari.Abstractions;
using Wasari.Crunchyroll.Abstractions;

namespace Wasari.Commands;

[Command("crunchy-list")]
internal class CrunchyrollListSeriesCommand : ICommand
{
    [CommandParameter(0, Description = "Series URL.")]
    public string SeriesUrl { get; init; }
        
    public CrunchyrollListSeriesCommand(ISeriesProvider<CrunchyrollSeasonsInfo> crunchyrollSeasonProvider)
    {
        CrunchyrollSeasonProvider = crunchyrollSeasonProvider;
    }

    private ISeriesProvider<CrunchyrollSeasonsInfo> CrunchyrollSeasonProvider { get; }
        
    public async ValueTask ExecuteAsync(IConsole console)
    {
        var seriesInfo = await CrunchyrollSeasonProvider.GetSeries(SeriesUrl);
        await console.Output.WriteLineAsync(seriesInfo.Name);
        
        foreach (var crunchyrollSeasonsInfo in seriesInfo.Seasons)
        {
            await console.Output.WriteLineAsync($"* [{crunchyrollSeasonsInfo.Season:00}] {crunchyrollSeasonsInfo.Title}");

            foreach (var crunchyrollEpisodeInfo in crunchyrollSeasonsInfo.Episodes)
            {
                await console.Output.WriteLineAsync($"\t [{crunchyrollEpisodeInfo.Number}] {crunchyrollEpisodeInfo.Name}");
            }
        }
    }
}