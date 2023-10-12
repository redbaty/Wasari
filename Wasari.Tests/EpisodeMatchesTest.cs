using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using Wasari.App;
using Wasari.Crunchyroll;
using Wasari.Tvdb.Api.Client;

namespace Wasari.Tests;

[TestClass]
public class EpisodeMatchesTest
{
    private IServiceProvider ServiceProvider { get; set; } = null!;

    private IWasariTvdbApi WasariTvdbApi { get; set; } = null!;

    private CrunchyrollApiService CrunchyrollApiService { get; set; } = null!;

    [TestInitialize]
    public void Setup()
    {
        var serviceProvider = BuildServiceProvider();
        ServiceProvider = serviceProvider;
        WasariTvdbApi = serviceProvider.GetRequiredService<IWasariTvdbApi>();
        CrunchyrollApiService = serviceProvider.GetRequiredService<CrunchyrollApiService>();
    }

    private static IServiceProvider BuildServiceProvider()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: "[{Timestamp:u} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(c => c.AddSerilog());
        serviceCollection.AddMemoryCache();
        serviceCollection.AddCrunchyrollServices();
        serviceCollection.AddWasariTvdbApi();
        serviceCollection.Configure<DownloadOptions>(e => { e.TryEnrichEpisodes = true; });
        return serviceCollection.BuildServiceProvider();
    }

    [TestMethod]
    [DataRow(
        "G6Q9GGE26" // I'm Standing on a Million Lives
        , 0
    )]
    [DataRow(
        "GRZXQJJ8Y" // The Ancient Magus' Bride
        , 4
    )]
    [DataRow(
        "GYEXQKJG6" // Dr. STONE
        , 1
    )]
    [DataRow(
        "GRDV0019R" // Jujustu Kaisen
        , 2
    )]
    [DataRow(
        "G4PH0WXVJ" // SPY x FAMILY
        , 0
    )]
    public async Task MatchAllEpisodes(string seriesId, int expectedNonEnrichedCount)
    {
        var episodes = await CrunchyrollApiService.GetAllEpisodes(seriesId)
            .Where(i => !i.IsDubbed)
            .ToArrayAsync();

        var enrichedEpisodes = await episodes.ToAsyncEnumerable()
            .EnrichWithWasariApi(ServiceProvider)
            .ToArrayAsync();

        Assert.AreEqual(episodes.Length, enrichedEpisodes.Length);

        var allEpisodesWereEnriched = enrichedEpisodes
            .Where(i => !i.WasEnriched)
            .ToList();

        Assert.AreEqual(expectedNonEnrichedCount, allEpisodesWereEnriched.Count, "Not all expected episodes were enriched");
    }
}