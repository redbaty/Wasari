using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Refit;

namespace Wasari.Tvdb;

public static class AppExtensions
{
    private static Uri EnsureTrailingSlash(this Uri uri)
    {
        var uriString = uri.ToString();
        if (!uriString.EndsWith("/"))
            uriString += "/";
        else
            return uri;
        return new Uri(uriString);
    }
    
    private static Uri EnsureNoTrailingSlash(this Uri uri)
    {
        var uriString = uri.ToString();
        if (uriString.EndsWith("/"))
            uriString = uriString[..^1];
        else
            return uri;
        
        return new Uri(uriString);
    }

    public static void AddTvdbServices(this IServiceCollection services)
    {
        var policy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(response => (int)response.StatusCode == 401) 
            .RetryAsync(3);
        
        var baseAddress = Environment.GetEnvironmentVariable("TVDB_API_URL") is { } baseUrl
            ? new Uri(baseUrl)
            : new Uri("https://api4.thetvdb.com/v4");

        services.AddMemoryCache();
        services.AddHttpClient<TvdbTokenHandler>(c => { c.BaseAddress = baseAddress.EnsureTrailingSlash(); });
        services.AddRefitClient<ITvdbApi>()
            .AddHttpMessageHandler<TvdbTokenHandler>()
            .ConfigureHttpClient(c => { c.BaseAddress = baseAddress.EnsureNoTrailingSlash(); })
            .AddPolicyHandler(policy);
    }
}