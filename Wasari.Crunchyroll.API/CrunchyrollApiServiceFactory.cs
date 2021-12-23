using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Wasari.Crunchyroll.API;

public class CrunchyrollApiServiceFactory
{
    public CrunchyrollApiServiceFactory(IServiceProvider provider, IMemoryCache cache, ILogger<CrunchyrollApiServiceFactory> logger)
    {
        Cache = cache;
        Logger = logger;
        CrunchyrollApiAuthenticationService = provider.GetService<CrunchyrollApiAuthenticationService>();
    }

    private CrunchyrollApiAuthenticationService CrunchyrollApiAuthenticationService { get; }

    private IMemoryCache Cache { get; }
    
    private ILogger<CrunchyrollApiServiceFactory> Logger { get; }

    public bool IsAuthenticated { get; private set; }

    private const string ChaveCache = "crunchyroll_service";

    public CrunchyrollApiService GetService() => Cache.Get<CrunchyrollApiService>(ChaveCache) ??
                                                 throw new InvalidOperationException("Service has not been created");

    public async Task CreateUnauthenticatedService()
    {
        var token = await CrunchyrollApiAuthenticationService.GetAccessToken();
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://beta-api.crunchyroll.com/"),
            DefaultRequestHeaders = { { "Authorization", $"Bearer {token}" } }
        };

        Logger.LogInformation("Created unauthenticated API service");
        Cache.Set(ChaveCache, new CrunchyrollApiService(httpClient));
    }

    public async Task CreateAuthenticatedService(string username, string password)
    {
        var token = await CrunchyrollApiAuthenticationService.GetAccessToken(username, password);
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://beta-api.crunchyroll.com/"),
            DefaultRequestHeaders = { { "Authorization", $"Bearer {token}" } }
        };

        Logger.LogInformation("Created authenticated API service");
        IsAuthenticated = true;
        Cache.Set(ChaveCache, new CrunchyrollApiService(httpClient));
    }
}