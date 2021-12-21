using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Wasari.Crunchyroll.API;

public class CrunchyrollApiServiceFactory
{
    public CrunchyrollApiServiceFactory(IServiceProvider provider, IMemoryCache cache)
    {
        Cache = cache;
        CrunchyrollApiAuthenticationService = provider.GetService<CrunchyrollApiAuthenticationService>();
    }

    private CrunchyrollApiAuthenticationService CrunchyrollApiAuthenticationService { get; }

    private IMemoryCache Cache { get; }

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

        IsAuthenticated = true;
        Cache.Set(ChaveCache, new CrunchyrollApiService(httpClient));
    }
}