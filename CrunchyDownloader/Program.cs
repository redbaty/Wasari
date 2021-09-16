using System;
using System.Threading.Tasks;
using CliFx;
using CrunchyDownloader.App;
using CrunchyDownloader.Commands;
using Microsoft.Extensions.DependencyInjection;
using PuppeteerSharp;
using Serilog;

namespace CrunchyDownloader
{
    internal static class Program
    {
        private static async Task<int> Main()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(
                    outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} <s:{SourceContext}>{NewLine}{Exception}")
                .CreateLogger();

            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();
            await using var browser = await Puppeteer.LaunchAsync(
                new LaunchOptions
                {
                    Headless = false,
#if RELEASE
                    Args = new[] {"--no-sandbox"}
#endif
                });
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(browser);
            serviceCollection.AddTransient<CrunchyRollAuthenticationService>();
            serviceCollection.AddTransient<YoutubeDlService>();
            serviceCollection.AddTransient<FfmpegService>();
            serviceCollection.AddTransient<CrunchyRollService>();
            serviceCollection.AddTransient<DownloadSeriesCommand>();
            serviceCollection.AddLogging(c => c.AddSerilog());
            var serviceProvider = serviceCollection.BuildServiceProvider();

            return await new CliApplicationBuilder()
                .AddCommand<DownloadSeriesCommand>()
                .UseTypeActivator(serviceProvider.GetService)
                .Build()
                .RunAsync();
        }
    }
}