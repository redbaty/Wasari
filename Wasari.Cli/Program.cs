using CliFx;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Wasari.Cli.Commands;

namespace Wasari.Cli;

internal static class Program
{
    private static async Task<int> Main()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();

        var serviceCollection = new ServiceCollection()
            .AddRootServices()
            .AddScoped<DownloadCommand>();
        var serviceProvider = serviceCollection.BuildServiceProvider();

        return await new CliApplicationBuilder()
            .AddCommand<DownloadCommand>()
            .UseTypeActivator(serviceProvider.GetService)
            .Build()
            .RunAsync();
    }
}