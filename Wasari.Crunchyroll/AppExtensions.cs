using Microsoft.Extensions.DependencyInjection;
using Wasari.Abstractions;
using Wasari.Crunchyroll.Abstractions;
using Wasari.Crunchyroll.API;
using Wasari.Ffmpeg;
using Wasari.YoutubeDl;

namespace Wasari.Crunchyroll
{
    public static class AppExtensions
    {
        public static void AddCrunchyrollServices(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<ISeriesDownloader<CrunchyrollEpisodeInfo>, CrunchyrollDownloader>();
            serviceCollection.AddTransient<BetaCrunchyrollService>();
            serviceCollection.AddTransient<YoutubeDlService>();
            serviceCollection.AddFfmpegServices();
            serviceCollection.AddTransient<YoutubeDlQueueFactoryService>();
            serviceCollection.AddSingleton<FfmpegQueueService>();
            serviceCollection.AddCrunchyrollApiServices();
        }
    }
}