using Microsoft.Extensions.DependencyInjection;

namespace Wasari.FFmpeg;

public static class FFmpegServiceExtensions
{
    public static void AddFfmpegServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped<FFmpegService>();
    }
}