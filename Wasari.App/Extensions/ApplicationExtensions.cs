using Microsoft.Extensions.DependencyInjection;
using Wasari.App.Abstractions;

namespace Wasari.App.Extensions;

public static class ApplicationExtensions
{
    public static IServiceCollection AddHostDownloader<T>(this IServiceCollection serviceCollection, string host) where T : class, IDownloadService
    {
        serviceCollection.Configure<DownloadOptions>(c =>
        {
            c.AddHostDownloader<T>(host);
        });
        serviceCollection.AddScoped<T>();
        return serviceCollection;
    }

    public static IServiceCollection AddDownloadServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped<GenericDownloadService>();
        serviceCollection.AddScoped<DownloadServiceSolver>();
        return serviceCollection;
    }
}