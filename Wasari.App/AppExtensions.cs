using Microsoft.Extensions.DependencyInjection;

namespace Wasari.App;

public static class AppExtensions
{
    public static void AddDownloadServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddTransient<SeriesProviderSolver>();
        serviceCollection.AddTransient<DownloadSeriesService>();
    }
}