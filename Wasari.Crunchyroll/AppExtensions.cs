using System;
using Microsoft.Extensions.DependencyInjection;
using Wasari.App;
using Wasari.App.Extensions;

namespace Wasari.Crunchyroll
{
    public static class AppExtensions
    {
        public static void AddCrunchyrollServices(this IServiceCollection serviceCollection)
        {
            var crunchyBaseAddres = new Uri("https://beta-api.crunchyroll.com/");
            serviceCollection.AddHttpClient<CrunchyrollAuthenticationHandler>(c =>
            {
                c.BaseAddress = crunchyBaseAddres;
                c.DefaultRequestHeaders.Add("Authorization",
                    "Basic a3ZvcGlzdXZ6Yy0teG96Y21kMXk6R21JSTExenVPVnRnTjdlSWZrSlpibzVuLTRHTlZ0cU8=");
            });
            serviceCollection.AddHttpClient<CrunchyrollApiService>(c => c.BaseAddress = crunchyBaseAddres)
                .AddHttpMessageHandler<CrunchyrollAuthenticationHandler>();
            serviceCollection.AddHostDownloader<CrunchyrollDownloadService>("beta.crunchyroll.com");
        }
    }
}