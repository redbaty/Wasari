using System.ComponentModel.DataAnnotations;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Wasari.App;
using Wasari.FFmpeg;
using Wasari.YoutubeDlp;

namespace Wasari.Cli.Commands;

[Command]
public class DownloadCommand : ICommand
{
    [CommandParameter(0, Description = "Series URL.", IsRequired = true)]
    public Uri SeriesUrl { get; init; }

    [CommandOption("output-directory", 'o')]
    public string OutputDirectory { get; init; } = Directory.GetCurrentDirectory();

    [CommandOption("username", 'u', Description = "Crunchyroll username.", EnvironmentVariable = "WASARI_USERNAME")]
    public string? Username { get; init; }

    [CommandOption("password", 'p', Description = "Crunchyroll password.", EnvironmentVariable = "WASARI_PASSWORD")]
    public string? Password { get; init; }
    
    [CommandOption("hevc", Description = "Encode final video file in H265/HEVC")]
    public bool UseHevc { get; init; } = true;
    
    [CommandOption("nvenc", Description = "Use NVENC encoding for FFmpeg encoding (Nvidia only)")]
    public bool UseNvenc { get; init; } = true;
    
    [CommandOption("dubs", Description = "Include all available dubs for each episode")]
    public bool IncludeDubs { get; init; } = false;
    
    [CommandOption("sub", Description = "Include all available subs for each episode")]
    public bool IncludeSubs { get; init; } = true;
    
    [CommandOption("skip", Description = "Skip files that already exists")]
    public bool SkipExistingFiles { get; init; } = true;

    [CommandOption("temp-encoding", Description = "Uses a temporary file for encoding, and moves it to final path at the end")]
    public bool UseTemporaryEncodingPath { get; init; } = true;

    [CommandOption("level-parallelism", 'l')]
    [Range(1, 10)]
    public int LevelOfParallelism { get; init; } = 2;

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var serviceCollection = new ServiceCollection().AddRootServices();
        serviceCollection.AddFfmpegServices();
        serviceCollection.AddYoutubeDlpServices();
        serviceCollection.AddScoped<DownloadService>();
        serviceCollection.Configure<DownloadOptions>(o =>
        {
            o.OutputDirectory = OutputDirectory;
            o.IncludeDubs = IncludeDubs;
            o.IncludeSubs = IncludeSubs;
            o.SkipExistingFiles = SkipExistingFiles;
        });
        serviceCollection.Configure<FFmpegOptions>(o =>
        {
            o.UseHevc = UseHevc;
            o.UseNvidiaAcceleration = UseNvenc;
            o.UseTemporaryEncodingPath = UseTemporaryEncodingPath;
        });
        serviceCollection.Configure<YoutubeDlpOptions>(o =>
        {
            o.Username = Username;
            o.Password = Password;
        });
        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        var downloadService = serviceProvider.GetRequiredService<DownloadService>();
        await downloadService.DownloadEpisodes(SeriesUrl.ToString(), LevelOfParallelism);
    }
}