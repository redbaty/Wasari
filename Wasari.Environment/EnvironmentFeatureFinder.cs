using Microsoft.Extensions.DependencyInjection;
using WasariEnvironment.Finders;

namespace WasariEnvironment;

public static class EnvironmentFeatureFinder
{
    public static async IAsyncEnumerable<EnvironmentFeature> GetEnvironmentFeatures()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTransient<IEnvironmentFeatureFinder, FfmpegFeatureFinder>();
        serviceCollection.AddTransient<IEnvironmentFeatureFinder, YtdlpFeatureFinder>();
        serviceCollection.AddTransient<IEnvironmentFeatureFinder, GpuFeatureFinder>();
        var provider = serviceCollection.BuildServiceProvider();
        
        var featureFinders = provider.GetServices<IEnvironmentFeatureFinder>();
        var tasks = featureFinders.Select(f => f.GetFeaturesAsync());
        var features = await Task.WhenAll(tasks);
        
        foreach (var feature in features.SelectMany(f => f).Distinct())
        {
            yield return feature;
        }
    }
}