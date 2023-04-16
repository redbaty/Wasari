using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Wasari.Tvdb.Api.Client;

public static class AppExtensions
{
    public static IServiceCollection AddWasariTvdbApi(this IServiceCollection services)
    {
        var wasariTvdbApiUrl = Environment.GetEnvironmentVariable("WasariTvdbApiUrl") ?? "https://wasari.mvmcj.com";
        
        services.AddRefitClient<IWasariTvdbApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(wasariTvdbApiUrl));
        return services;
    }
}