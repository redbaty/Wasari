using Microsoft.Extensions.DependencyInjection;
using Wasari.App.Abstractions;

namespace Wasari.YoutubeDlp;

public static class YoutubeDlpExtensions
{
    public static void AddYoutubeDlpServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped<YoutubeDlpService>();
    }
}