using CliFx;
using Figgle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasari.Cli.Commands;
using WasariEnvironment;

namespace Wasari.Cli;

internal static class Program
{
    private static async Task<int> Main()
    {
        await Console.Error.WriteLineAsync(FiggleFonts.Standard.Render("Wasari"));
        
        var serviceCollection = await new ServiceCollection()
            .AddRootServices();
        serviceCollection.AddScoped<DownloadCommand>();
        serviceCollection.AddSingleton<ShaderConverter>();
        serviceCollection.AddSingleton<ResolutionConverter>();
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
            .SetTitle("Wasari")
            .SetDescription("Downloads anime episodes from various sources")
            .AddCommand<DownloadCommand>()
            .UseTypeActivator(serviceProvider.GetService)
            .Build()
            .RunAsync();
    }
}