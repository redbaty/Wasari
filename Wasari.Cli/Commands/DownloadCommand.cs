using System.ComponentModel.DataAnnotations;
using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using CliWrap;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wasari.App;
using Wasari.App.Abstractions;
using Wasari.App.Extensions;
using Wasari.Cli.Converters;
using Wasari.Cli.Services;
using Wasari.Crunchyroll;
using Wasari.FFmpeg;
using Wasari.Tvdb.Api.Client;
using Wasari.YoutubeDlp;
using WasariEnvironment;

namespace Wasari.Cli.Commands;

[Command]
public class DownloadCommand : ICommand
{
    public DownloadCommand(EnvironmentService environmentService, ILogger<DownloadCommand> logger)
    {
        EnvironmentService = environmentService;
        Logger = logger;
    }

    [CommandParameter(0, Description = "Series, season or episode URL.", IsRequired = true)]
    public Uri? Url { get; init; }

    [CommandOption("output-directory", 'o', EnvironmentVariable = "OUTPUT_DIRECTORY")]
    public string OutputDirectory { get; init; } = Environment.GetEnvironmentVariable("DEFAULT_OUTPUT_DIRECTORY") ?? Directory.GetCurrentDirectory();

    [CommandOption("username", 'u', Description = "Username", EnvironmentVariable = "WASARI_USERNAME")]
    public string? Username { get; init; }

    [CommandOption("password", 'p', Description = "Password", EnvironmentVariable = "WASARI_PASSWORD")]
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

    [CommandOption("level-parallelism", 'l', Description = "Defines how many downloads are going to run in parallel")]
    [Range(1, 10)]
    public int LevelOfParallelism { get; init; } = 2;

    [CommandOption("episodes", 'e', Description = "Episodes range (eg. 1-5 would be episode 1 to 5)")]
    public string? EpisodeRange { get; init; }

    [CommandOption("seasons", 's', Description = "Seasons range (eg. 1-3 would be seasons 1 to 5)")]
    public string? SeasonsRange { get; init; }

    [CommandOption("no-update", Description = "Do not try to update yt-dlp")]
    public bool NoUpdate { get; init; }

    [CommandOption("series-folder", Description = "Creates a sub-directory with the series name")]
    public bool CreateSeriesFolder { get; init; } = true;

    [CommandOption("season-folder", Description = "Creates a sub-directory with the season number")]
    public bool CreateSeasonFolder { get; init; } = true;

    [CommandOption("verbose", 'v', Description = "Sets the logging level to verbose (Helps with FFmpeg debug)")]
    public bool Verbose { get; init; }

    [CommandOption("shader", Converter = typeof(ShaderConverter))]
    public IFFmpegShader[]? Shaders { get; init; }

    [CommandOption("resolution", 'r', Converter = typeof(ResolutionConverter))]
    public FFmpegResolution? Resolution { get; init; }

    [CommandOption("format", 'f', Description = "Format passed to yt-dlp")]
    public string? Format { get; init; }

    [CommandOption("ignore-tls")]
    public bool IgnoreTls { get; init; }

    [CommandOption("enrich-episodes", Description = "If true, will try to enrich episodes with metadata from TVDB (Fixes season and episode numbers)")]
    public bool EnrichEpisodes { get; init; } = true;

    [CommandOption("ffmpeg-threads", Description = "Number of threads to use for FFmpeg")]
    public int? FfmpegThreads { get; init; }

    [CommandOption("skip-unique-episode-check", 'S', Description = "If true, will skip the check for unique episodes")]
    public bool SkipUniqueEpisodeCheck { get; init; }

    [CommandOption("webhook-url", Description = "Webhook URL to send notifications to", EnvironmentVariable = "WASARI_WEBHOOK_URL")]
    public Uri? WebhookUrl { get; init; }

    private EnvironmentService EnvironmentService { get; }

    private ILogger<DownloadCommand> Logger { get; }

    private static Ranges? ParseRange(string? range)
    {
        if (string.IsNullOrEmpty(range))
            return null;

        if (range.Any(i => !char.IsDigit(i) && i != '-'))
            throw new InvalidRangeException();

        if (range.Contains('-'))
        {
            var episodesNumbers = range.Split('-');

            if (episodesNumbers.Length != 2 || episodesNumbers.All(string.IsNullOrEmpty))
                throw new InvalidRangeException();

            var numbers = episodesNumbers.Select(i => int.TryParse(i, out var n) ? n : (int?)null).ToArray();

            if (episodesNumbers.All(i => !string.IsNullOrEmpty(i)))
                return new Ranges(numbers.ElementAtOrDefault(0), numbers.ElementAtOrDefault(1));

            if (string.IsNullOrEmpty(episodesNumbers[0]))
                return new Ranges(null, numbers.ElementAtOrDefault(1));

            if (string.IsNullOrEmpty(episodesNumbers[1]))
                return new Ranges(numbers.ElementAtOrDefault(0), null);
        }

        if (int.TryParse(range, out var episode)) return new Ranges(episode, episode);

        throw new InvalidOperationException($"Invalid episode range. {range}");
    }

    private static Task<CommandResult> TryYtdlpUpdate()
    {
        var command = CliWrap.Cli.Wrap("yt-dlp")
            .WithArguments("-U")
            .WithStandardOutputPipe(PipeTarget.ToDelegate(Console.Error.WriteLine))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(Console.Error.WriteLine))
            .WithValidation(CommandResultValidation.None);
        return command.ExecuteAsync();
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (Url == null)
        {
            throw new CommandException("SeriesURL is required");
        }

        var missingEnvironmentFeatures = EnvironmentService.GetMissingFeatures(EnvironmentFeatureType.Ffmpeg, EnvironmentFeatureType.YtDlp).ToArray();
        if (missingEnvironmentFeatures.Length > 0)
        {
            throw new CommandException($"One or more environment features are missing. ({missingEnvironmentFeatures.Select(i => i.ToString()).Aggregate((x, y) => $"{x},{y}")})");
        }

        if (!NoUpdate)
        {
            var ytDlpCommandResult = await TryYtdlpUpdate();

            if (ytDlpCommandResult.ExitCode == 0)
                Logger.LogInformation("YT-DLP is up-to-date");
            else
                Logger.LogError("Failed to update YT-DLP");
        }

        Logger.LogInformation("Output directory is {@OutputDirectory}", OutputDirectory);

        if (Verbose)
        {
            Environment.SetEnvironmentVariable("LOG_LEVEL", "0");
        }

        var serviceCollection = await new ServiceCollection().AddRootServices();
        serviceCollection.AddFfmpegServices();
        serviceCollection.AddYoutubeDlpServices();
        serviceCollection.AddDownloadServices();
        serviceCollection.AddCrunchyrollServices();
        serviceCollection.AddMemoryCache();
        serviceCollection.AddWasariTvdbApi();
        serviceCollection.Configure<DownloadOptions>(o =>
        {
            o.OutputDirectory = OutputDirectory;
            o.IncludeDubs = IncludeDubs;
            o.IncludeSubs = IncludeSubs;
            o.SkipExistingFiles = SkipExistingFiles;
            o.EpisodesRange = ParseRange(EpisodeRange);
            o.SeasonsRange = ParseRange(SeasonsRange);
            o.CreateSeriesFolder = CreateSeriesFolder;
            o.CreateSeasonFolder = CreateSeasonFolder;
            o.TryEnrichEpisodes = EnrichEpisodes;
            o.SkipUniqueEpisodeCheck = SkipUniqueEpisodeCheck;
        });
        serviceCollection.Configure<FFmpegOptions>(o =>
        {
            o.UseHevc = UseHevc;
            o.UseNvidiaAcceleration = UseNvenc;
            o.UseTemporaryEncodingPath = UseTemporaryEncodingPath;
            o.Shaders = Shaders;
            o.Resolution = Resolution;
            o.Threads = FfmpegThreads;
        });
        serviceCollection.Configure<AuthenticationOptions>(o =>
        {
            o.Username = Username;
            o.Password = Password;
        });
        serviceCollection.Configure<YoutubeDlpOptions>(c =>
        {
            c.Format = Format;
            c.IgnoreTls = IgnoreTls;
        });

        if (WebhookUrl != null)
            serviceCollection.AddHttpClient<NotificationService>(c => { c.BaseAddress = WebhookUrl; });

        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        var downloadService = serviceProvider.GetRequiredService<DownloadServiceSolver>();
        var downloadedEpisodes = await downloadService.GetService(Url).DownloadEpisodes(Url.ToString(), LevelOfParallelism);

        if (serviceProvider.GetService<NotificationService>() is { } notificationService)
        {
            await notificationService.SendNotifcationForDownloadedEpisodeAsync(downloadedEpisodes);
        }
    }
}