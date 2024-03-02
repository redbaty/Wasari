using Microsoft.AspNetCore.OutputCaching;

namespace Wasari.Tvdb.Api.Policies;

public class EpisodeCachePolicy : IOutputCachePolicy
{
    public static readonly EpisodeCachePolicy Instance = new();

    private EpisodeCachePolicy()
    {
    }
    
    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        var attemptOutputCaching = AttemptOutputCaching();
        context.EnableOutputCaching = true;
        context.AllowCacheLookup = attemptOutputCaching;
        context.AllowCacheStorage = attemptOutputCaching;
        context.AllowLocking = true;
        
        var value = context.HttpContext.Request.Query["query"].ToString().Trim();
        context.CacheVaryByRules.VaryByValues.Add(new KeyValuePair<string, string>("query", value));

        context.ResponseExpirationTimeSpan = TimeSpan.FromMinutes(5);
        return ValueTask.CompletedTask;
    }

    ValueTask IOutputCachePolicy.ServeFromCacheAsync
        (OutputCacheContext context, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    ValueTask IOutputCachePolicy.ServeResponseAsync
        (OutputCacheContext context, CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;
        
        if (response.StatusCode != StatusCodes.Status200OK && response.StatusCode != StatusCodes.Status400BadRequest)
        {
            context.AllowCacheStorage = false;
            return ValueTask.CompletedTask;
        }
        
        return ValueTask.CompletedTask;
    }

    private static bool AttemptOutputCaching() => true;
}