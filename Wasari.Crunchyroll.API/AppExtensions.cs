using System;
using Microsoft.Extensions.DependencyInjection;

namespace Wasari.Crunchyroll.API
{
    public static class AppExtensions
    {
        public static void AddCrunchyrollApiServices(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddHttpClient<CrunchyrollApiAuthenticationService>(c =>
            {
                c.BaseAddress = new Uri("https://beta-api.crunchyroll.com/");
                c.DefaultRequestHeaders.Add("Authorization",
                    "Basic a3ZvcGlzdXZ6Yy0teG96Y21kMXk6R21JSTExenVPVnRnTjdlSWZrSlpibzVuLTRHTlZ0cU8=");
            });

            serviceCollection.AddSingleton<CrunchyrollApiServiceFactory>();
        }
    }
}