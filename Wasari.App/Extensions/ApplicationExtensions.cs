using Microsoft.Extensions.DependencyInjection;
using Wasari.App.Abstractions;
using Wasari.FFmpeg;
using Wasari.YoutubeDlp;

namespace Wasari.App.Extensions;

public static class ApplicationExtensions
{
    public static IServiceCollection AddHostDownloader<T>(this IServiceCollection serviceCollection, params string[] hosts) where T : class, IDownloadService
    {
        serviceCollection.Configure<DownloadOptions>(c =>
        {
            foreach (var host in hosts) c.AddHostDownloader<T>(host);
        });
        serviceCollection.AddScoped<T>();
        return serviceCollection;
    }

    public static IServiceCollection AddDownloadServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddFfmpegServices();
        serviceCollection.AddYoutubeDlpServices();
        serviceCollection.AddScoped<GenericDownloadService>();
        serviceCollection.AddScoped<DownloadServiceSolver>();
        return serviceCollection;
    }
}