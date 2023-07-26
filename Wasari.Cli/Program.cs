using System.Reflection;
using CliFx;
using Figgle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasari.Anime4k;
using Wasari.Cli.Commands;
using Wasari.Cli.Converters;
using Wasari.FFmpeg;
using WasariEnvironment;

namespace Wasari.Cli;

internal static class Program
{
    private static async Task<int> Main()
    {
        await Console.Error.WriteLineAsync(FiggleFonts.Standard.Render("Wasari"));
        await Console.Error.WriteLineAsync($"Current version: {Assembly.GetExecutingAssembly().GetName().Version}");
        
        var serviceCollection = await new ServiceCollection()
            .AddRootServices();
        serviceCollection.AddScoped<DownloadCommand>();
        serviceCollection.AddSingleton<ShaderConverter>();
        serviceCollection.AddSingleton<ResolutionConverter>();
        serviceCollection.AddAnime4KShader();
        serviceCollection.Configure<FFmpegResolutionPresets>(c =>
        {
            c.Presets.Add("4k", FFmpegResolution.FourK);
        });
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var environmentOptions = serviceProvider.GetService<IOptions<EnvironmentOptions>>();

        if (environmentOptions?.Value?.Features is { } features)
        {
            await Console.Error.WriteLineAsync($"Available environment features: {features.Select(i => $"\"{i.Type}\"").Aggregate((x, y) => $"{x}, {y}")}");
        }

        return await new CliApplicationBuilder()
            .SetExecutableName("Wasari.Cli")
            .SetTitle("Wasari")
            .SetDescription("Downloads anime episodes from various sources")
            .AddCommand<DownloadCommand>()
            .UseTypeActivator(serviceProvider.GetRequiredService)
            .Build()
            .RunAsync();
    }
}