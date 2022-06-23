using System;
using System.Linq;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Microsoft.Extensions.Logging;
using Wasari.Abstractions;
using Wasari.App;
using Wasari.Crunchyroll.API;

namespace Wasari.Commands;

[Command("crunchy-list")]
internal class CrunchyrollListSeriesCommand : AuthenticatedCommand, ICommand
{
    [CommandParameter(0, Description = "Series URL.")]
    public Uri SeriesUrl { get; init; }
        
    public CrunchyrollListSeriesCommand(CrunchyrollApiServiceFactory crunchyrollApiServiceFactory, ILogger<CrunchyrollListSeriesCommand> logger, SeriesProviderSolver seriesProviderSolver, IServiceProvider serviceProvider) : base(crunchyrollApiServiceFactory)
    {
        Logger = logger;
        SeriesProviderSolver = seriesProviderSolver;
        ServiceProvider = serviceProvider;
    }
    
    private SeriesProviderSolver SeriesProviderSolver { get; }
    
    private ILogger<CrunchyrollListSeriesCommand> Logger { get; }

    private static readonly EventId EpisodesFoundEvent = new(200, "Episodes found");
    
    private static readonly EventId EpisodesNotFoundEvent = new(404, "No episodes found");
    
    private IServiceProvider ServiceProvider { get; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        await AuthenticateCrunchyroll();
        
        if (!CrunchyrollApiServiceFactory.IsAuthenticated)
        {
            Logger.LogWarning("Using unauthenticated API, results might be incomplete");
        }
        
        var seriesProviderType = SeriesProviderSolver.GetProvider(SeriesUrl);

        if (ServiceProvider.GetService(seriesProviderType) is not ISeriesProvider seriesProvider)
            throw new InvalidOperationException($"Failed to create series provider. Type: {seriesProviderType.Name}");
        
        var episodes = await seriesProvider.GetEpisodes(SeriesUrl.ToString())
            .Where(i => !i.SeasonInfo.Dubbed)
            .Select(i => new
            {
                i.Id,
                i.Name,
                i.FilePrefix,
                i.SeriesInfo,
                Season = new
                {
                    i.SeasonInfo.Dubbed,
                    i.SeasonInfo.Season,
                    i.SeasonInfo.Title,
                    i.SeasonInfo.DubbedLanguage
                }
            })
            .ToArrayAsync();

        if (episodes.Any())
            Logger.LogInformation(EpisodesFoundEvent, "Crunchyroll episodes found {Count} {@Episodes}", episodes.Length, episodes);
        else
            Logger.LogError(EpisodesNotFoundEvent, "No episodes found in crunchyroll {Url}", SeriesUrl);
    }
}