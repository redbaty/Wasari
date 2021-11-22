using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using PuppeteerExtraSharp;
using PuppeteerExtraSharp.Plugins.ExtraStealth;
using PuppeteerSharp;
using Wasari.Abstractions;
using Wasari.Crunchyroll.Abstractions;
using Wasari.Ffmpeg;

namespace Crunchyroll.API
{
    public static class AppExtensions
    {
        public static async Task AddCrunchyrollServices(this IServiceCollection serviceCollection)
        {
            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();
            var extra = new PuppeteerExtra();
            extra.Use(new StealthPlugin());

            var browser = await extra.LaunchAsync(
                new LaunchOptions
                {
                    Headless = true,
#if RELEASE
                    Args = new[] {"--no-sandbox"}
#endif
                });

            serviceCollection.AddSingleton(browser);
            serviceCollection.AddTransient<ISeriesDownloader<CrunchyrollEpisodeInfo>, CrunchyrollDownloader>();
            serviceCollection.AddTransient<ISeriesProvider<CrunchyrollSeasonsInfo>, CrunchyRollService>();
            serviceCollection.AddTransient<YoutubeDlService>();
            serviceCollection.AddFfmpegServices();
            serviceCollection.AddSingleton<YoutubeDlQueueService>();
            serviceCollection.AddSingleton<FfmpegQueueService>();
            serviceCollection.AddHttpClient<CrunchyrollApiAuthenticationService>(c =>
            {
                c.BaseAddress = new Uri("https://beta-api.crunchyroll.com/");
                c.DefaultRequestHeaders.Add("Authorization",
                    "Basic a3ZvcGlzdXZ6Yy0teG96Y21kMXk6R21JSTExenVPVnRnTjdlSWZrSlpibzVuLTRHTlZ0cU8=");
            });
            serviceCollection.AddHttpClient<CrunchyrollApiService>(c =>
            {
                c.BaseAddress = new Uri("https://beta-api.crunchyroll.com/");
            });
        }
    }
}