using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using Wasari.Abstractions;
using Wasari.Abstractions.Extensions;
using Wasari.App;
using Wasari.Crunchyroll;
using Wasari.Crunchyroll.Abstractions;
using Wasari.Crunchyroll.API;
using Wasari.Exceptions;
using Wasari.Ffmpeg;
using Wasari.Models;
using WasariEnvironment;

namespace Wasari.Commands
{
    [Command("crunchy")]
    internal class CrunchyrollDownloadSeriesCommand : CommonDownloadCommand, ICommand
    {
        public CrunchyrollDownloadSeriesCommand(
            CrunchyRollAuthenticationService crunchyRollAuthenticationService,
            ILogger<CrunchyrollDownloadSeriesCommand> logger,
            ISeriesProvider<CrunchyrollSeasonsInfo> crunchyrollSeasonProvider,
            ISeriesDownloader<CrunchyrollEpisodeInfo> crunchyrollDownloader,
            EnvironmentService environmentService, BetaCrunchyrollService betaCrunchyrollService, BrowserFactory browserFactory, CrunchyrollApiServiceFactory crunchyrollApiServiceFactory)
        {
            CrunchyRollAuthenticationService = crunchyRollAuthenticationService;
            Logger = logger;
            CrunchyrollSeasonProvider = crunchyrollSeasonProvider;
            CrunchyrollDownloader = crunchyrollDownloader;
            EnvironmentService = environmentService;
            BetaCrunchyrollService = betaCrunchyrollService;
            BrowserFactory = browserFactory;
            CrunchyrollApiServiceFactory = crunchyrollApiServiceFactory;
        }

        private CrunchyRollAuthenticationService CrunchyRollAuthenticationService { get; }

        [CommandParameter(0, Description = "Series URL.")]
        public string SeriesUrl { get; init; }

        [CommandOption("username", 'u', Description = "Crunchyroll username.")]
        public string Username { get; init; }

        [CommandOption("password", 'p', Description = "Crunchyroll password.")]
        public string Password { get; init; }

        [CommandOption("episodes", 'e')]
        public string EpisodeRange { get; init; }

        [CommandOption("seasons", 's')]
        public string SeasonsRange { get; init; }

        [CommandOption("preset")]
        public string ConversionPreset { get; init; }

        [CommandOption("clean")]
        public bool CleanTemporaryFiles { get; init; } = true;

        [CommandOption("hevc")]
        public bool ConvertToHevc { get; init; } = true;

        [CommandOption("haccel", 'a')]
        public bool HardwareAcceleration { get; init; } = true;

        [CommandOption("gpuaccel")]
        public bool GpuAcceleration { get; init; } = true;

        [CommandOption("temp-dir", 't')]
        public string TemporaryDirectory { get; init; } = Path.Combine(Path.GetTempPath(), "Wasari");

        [CommandOption("skip-episodes")]
        public bool SkipExistingEpisodes { get; init; } = true;

        private ILogger<CrunchyrollDownloadSeriesCommand> Logger { get; }

        private ISeriesProvider<CrunchyrollSeasonsInfo> CrunchyrollSeasonProvider { get; }

        private ISeriesDownloader<CrunchyrollEpisodeInfo> CrunchyrollDownloader { get; }

        private EnvironmentService EnvironmentService { get; }

        private BetaCrunchyrollService BetaCrunchyrollService { get; }

        private BrowserFactory BrowserFactory { get; }
        
        private CrunchyrollApiServiceFactory CrunchyrollApiServiceFactory { get; }

        public async ValueTask ExecuteAsync(IConsole console)
        {
            EnvironmentService.ThrowIfFeatureNotAvailable(EnvironmentFeature.Ffmpeg, EnvironmentFeature.YtDlp);

            if (string.IsNullOrEmpty(Username) && string.IsNullOrEmpty(Password))
            {
                await CrunchyrollApiServiceFactory.CreateUnauthenticatedService();
            }
            else
            {
                if (string.IsNullOrEmpty(Username))
                    throw new CrunchyrollAuthenticationException("Missing username", Username, Password);

                if (string.IsNullOrEmpty(Password))
                    throw new CrunchyrollAuthenticationException("Missing password", Username, Password);

                await CrunchyrollApiServiceFactory.CreateAuthenticatedService(Username, Password);
            }
            
            var stopwatch = Stopwatch.StartNew();
            var isValidSeriesUrl = IsValidSeriesUrl();
            var isBeta = SeriesUrl.Contains("beta.");

            if (isBeta)
            {
                Logger.LogInformation("BETA Series detected");
            }

            if (!isValidSeriesUrl)
                throw new CommandException("The URL provided doesnt seem to be a crunchyroll SERIES page URL.");

            using var cookieFile = isBeta ? null : await CreateCookiesFile();
            var seriesInfo = isBeta ? await BetaCrunchyrollService.GetSeries(SeriesUrl) : await CrunchyrollSeasonProvider.GetSeries(SeriesUrl);

            var episodes = seriesInfo.Seasons
                .SelectMany(i => i.Episodes)
                .OrderBy(i => i.SeasonInfo.Season)
                .ThenBy(i => i.Number)
                .ToList();
            
            await BrowserFactory.DisposeAsync();

            if (!episodes.Any())
            {
                Logger.LogWarning("No episodes found");
                return;
            }

            var seasonsRange = ParseRange(SeasonsRange, episodes.Select(i => i.SeasonInfo.Season).Max());
            Logger.LogInformation("Seasons range is {@Range}", seasonsRange);
            episodes = episodes.Where(i =>
                    i.SeasonInfo.Season >= seasonsRange[0]
                    && i.SeasonInfo.Season <= seasonsRange[1])
                .ToList();

            var episodeRange = ParseRange(EpisodeRange, (int)episodes.Select(i => i.SequenceNumber).Max());
            Logger.LogInformation("Episodes range is {@Range}", episodeRange);
            episodes = episodes.Where(i =>
                    i.SequenceNumber >= episodeRange[0]
                    && i.SequenceNumber <= episodeRange[1])
                .ToList();
            
            if (episodes.Any(i => i.Premium) && !CrunchyrollApiServiceFactory.IsAuthenticated)
                throw new CommandException("Premium only episodes encountered, but no credentials were provided.");

            var downloadParameters = await CreateDownloadParameters(cookieFile, seriesInfo);
            if (SkipExistingEpisodes) FilterExistingEpisodes(downloadParameters.OutputDirectory, episodes);

            if (!episodes.Any())
            {
                Logger.LogWarning("No episodes found");
            }
            else
            {
                var downloadedFiles = await CrunchyrollDownloader.DownloadEpisodes(episodes, downloadParameters)
                    .ToArrayAsync();

                foreach (var downloadedFile in downloadedFiles)
                    Logger.LogDebug("File downloaded to path: {@FilePath} {@Type}", downloadedFile.Path,
                        downloadedFile.Type);
            }

            if (cookieFile != null)
            {
                Logger.LogDebug("Cleaning cookie file {@CookieFile}", cookieFile);
                cookieFile?.Dispose();
            }

            stopwatch.Stop();
            Logger.LogInformation("Completed. Time Elapsed {@TimeElapsed}", stopwatch.Elapsed);
        }

        private async Task<TemporaryCookieFile> CreateCookiesFile()
        {
            if (string.IsNullOrEmpty(Username) && string.IsNullOrEmpty(Password)) return null;

            if (string.IsNullOrEmpty(Username))
                throw new CrunchyrollAuthenticationException("Missing username", Username, Password);

            if (string.IsNullOrEmpty(Password))
                throw new CrunchyrollAuthenticationException("Missing password", Username, Password);

            var cookies = await CrunchyRollAuthenticationService.GetCookies(Username, Password);
            var cookieFileName = Path.GetTempFileName();
            await File.WriteAllTextAsync(cookieFileName, cookies);

            Logger.LogInformation("Cookie file written to {@FilePath}", cookieFileName);

            return new TemporaryCookieFile { Path = cookieFileName };
        }

        private void FilterExistingEpisodes(string outputDirectory, List<CrunchyrollEpisodeInfo> episodes)
        {
            const string regex = @"S(?<season>\d+)E(?<episode>\d+) -";

            foreach (var episodeFile in Directory.GetFiles(outputDirectory, "*.mkv"))
            {
                var episodeMatch = Regex.Match(episodeFile, regex);

                if (!episodeMatch.Success
                    || !int.TryParse(episodeMatch.Groups["episode"].Value, out var episode)
                    || !int.TryParse(episodeMatch.Groups["season"].Value, out var season)) continue;
                
                var removed = episodes.RemoveAll(i => i.SeasonInfo.Season == season && i.SequenceNumber == episode);

                if (removed > 0)
                {
                    Logger.LogWarning(
                        "Skipping episode {@EpisodeNumber} from season {@SeasonNumber} due to existing file {@FilePath}",
                        episode, season, episodeFile);
                }
            }
        }

        private bool IsValidSeriesUrl()
        {
            if (Uri.TryCreate(SeriesUrl, UriKind.Absolute, out var parsedUri))
            {
                var crunchyHost =
                    parsedUri.Host.EndsWith("crunchyroll.com", StringComparison.InvariantCultureIgnoreCase);
                var rightSegmentsCount = parsedUri.Segments.Length >= 2;
                return crunchyHost && rightSegmentsCount;
            }

            return true;
        }

        private async Task<DownloadParameters> CreateDownloadParameters(TemporaryCookieFile file,
            ISeriesInfo seriesInfo)
        {
            var isNvidiaAvailable = GpuAcceleration && await FfmpegService.IsNvidiaAvailable() && EnvironmentService.IsFeatureAvailable(EnvironmentFeature.NvidiaGpu);

            if (isNvidiaAvailable) Logger.LogInformation("NVIDIA hardware acceleration is available");

            var safeSeriesName = seriesInfo.Name.AsSafePath();

            var outputDirectory = CreateSubdirectory
                ? Path.Combine(OutputDirectory, safeSeriesName)
                : OutputDirectory;

            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            return new DownloadParameters
            {
                CookieFilePath = file?.Path,
                SubtitleLanguage = SubtitleLanguage,
                Subtitles = !string.IsNullOrEmpty(SubtitleLanguage) || Subtitles,
                OutputDirectory = outputDirectory,
                CreateSeasonFolder = CreateSeasonFolder,
                UseNvidiaAcceleration = isNvidiaAvailable,
                UseHardwareAcceleration = HardwareAcceleration,
                ConversionPreset = ConversionPreset,
                DeleteTemporaryFiles = CleanTemporaryFiles,
                UseHevc = ConvertToHevc,
                TemporaryDirectory = TemporaryDirectory,
                ParallelDownloads = DownloadPoolSize,
                ParallelMerging = EncodingPoolSize
            };
        }

        private static int[] ParseRange(string range, int max)
        {
            if (string.IsNullOrEmpty(range))
                return new[] { 0, max };

            if (range.Any(i => !char.IsDigit(i) && i != '-'))
                throw new InvalidEpisodeRangeException();

            if (range.Contains('-'))
            {
                var episodesNumbers = range.Split('-');

                if (episodesNumbers.Length != 2 || episodesNumbers.All(string.IsNullOrEmpty))
                    throw new InvalidEpisodeRangeException();

                if (episodesNumbers.All(i => !string.IsNullOrEmpty(i)))
                    return episodesNumbers.Select(int.Parse).ToArray();

                if (string.IsNullOrEmpty(episodesNumbers[0]))
                    return new[] { 0, int.Parse(episodesNumbers[1]) };

                if (string.IsNullOrEmpty(episodesNumbers[1]))
                    return new[] { int.Parse(episodesNumbers[0]), max };
            }

            if (int.TryParse(range, out var episode)) return new[] { episode, episode };

            throw new InvalidOperationException($"Invalid episode range. {range}");
        }
    }
}