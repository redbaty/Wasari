using Microsoft.Extensions.DependencyInjection;
using Wasari.FFmpeg;

namespace Wasari.Anime4k;

public static class AppExtensions
{
    public static void AddAnime4KShader(this IServiceCollection serviceCollection)
    {
        serviceCollection.Configure<FFmpegShaderPresets>(c =>
        {
            c.ShadersFactory.Add("anime4k", _ => new Anime4KShader());
        });
    }
}