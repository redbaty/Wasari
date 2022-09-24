using Microsoft.Extensions.DependencyInjection;

namespace Wasari.YoutubeDlp;

public static class YoutubeDlpExtensions
{
    public static void AddYoutubeDlpServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped<YoutubeDlpService>();
    }
}