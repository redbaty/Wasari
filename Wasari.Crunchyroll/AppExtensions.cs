using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Wasari.Abstractions;
using Wasari.Crunchyroll.Abstractions;
using Wasari.Crunchyroll.API;
using Wasari.Ffmpeg;

namespace Wasari.Crunchyroll
{
    public static class AppExtensions
    {
        public static void AddCrunchyrollServices(this IServiceCollection serviceCollection)
        {
            Log.Logger.Information("Setting up chromium...");

         

             Log.Logger.Information("Chromium set up successfully");

            serviceCollection.AddSingleton<BrowserFactory>();
            serviceCollection.AddTransient<ISeriesDownloader<CrunchyrollEpisodeInfo>, CrunchyrollDownloader>();
            serviceCollection.AddTransient<ISeriesProvider<CrunchyrollSeasonsInfo>, CrunchyrollService>();
            serviceCollection.AddTransient<BetaCrunchyrollService>();
            serviceCollection.AddTransient<YoutubeDlService>();
            serviceCollection.AddFfmpegServices();
            serviceCollection.AddSingleton<YoutubeDlQueueService>();
            serviceCollection.AddSingleton<FfmpegQueueService>();
            serviceCollection.AddCrunchyrollApiServices();
        }
    }
}