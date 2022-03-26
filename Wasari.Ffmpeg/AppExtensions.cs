using Microsoft.Extensions.DependencyInjection;

namespace Wasari.Ffmpeg
{
    public static class AppExtensions
    {
        public static void AddFfmpegServices(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<FfmpegService>();
            serviceCollection.AddTransient<FfprobeService>();
        }
    }
}