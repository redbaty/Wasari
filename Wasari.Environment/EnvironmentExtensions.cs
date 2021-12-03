using Microsoft.Extensions.DependencyInjection;

namespace WasariEnvironment;

public static class EnvironmentExtensions
{
    public static async Task AddEnvironmentServices(this IServiceCollection services)
    {
        var features = await EnvironmentFeatureFinder.GetEnvironmentFeatures().ToArrayAsync();
        services.Configure<EnvironmentOptions>(o => o.Features = features);
        services.AddTransient<EnvironmentService>();
    }
}