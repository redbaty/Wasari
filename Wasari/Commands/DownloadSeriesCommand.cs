using System;
using System.Collections.Generic;
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
using Wasari.Crunchyroll.Abstractions;
using Wasari.Exceptions;
using Wasari.Ffmpeg;
using Wasari.Models;

namespace Wasari.Commands
{
    [Command]
    internal class DownloadSeriesCommand : CrunchyAuthenticatedCommand, ICommand
    {
        public DownloadSeriesCommand(
            CrunchyRollAuthenticationService crunchyRollAuthenticationService, Browser browser,
            ILogger<DownloadSeriesCommand> logger, ISeriesProvider<CrunchyrollSeasonsInfo> crunchyrollSeasonProvider, ISeriesDownloader<CrunchyrollEpisodeInfo> crunchyrollDownloader) : base(
            crunchyRollAuthenticationService, logger)
        {
            Browser = browser;
            Logger = logger;
            CrunchyrollSeasonProvider = crunchyrollSeasonProvider;
            CrunchyrollDownloader = crunchyrollDownloader;
        }

        [CommandParameter(0, Description = "Series URL.")]
        public string SeriesUrl { get; init; }

        [CommandOption("create-subdir", 'c')]
        public bool CreateSubdirectory { get; init; } = true;

        [CommandOption("output-directory", 'o')]
        public string OutputDirectory { get; init; } = Directory.GetCurrentDirectory();

        [CommandOption("sub")]
        public bool Subtitles { get; init; } = true;

        [CommandOption("sub-language", 'l')]
        public string SubtitleLanguage { get; init; }

        [CommandOption("batch", 'b')]
        public int EpisodeBatchSize { get; init; } = 3;
        
        public int ParallelDownloadPoolSize { get; init; } = 3;

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

        private ILogger<DownloadSeriesCommand> Logger { get; }
        
        private ISeriesProvider<CrunchyrollSeasonsInfo> CrunchyrollSeasonProvider { get; }
        
        private ISeriesDownloader<CrunchyrollEpisodeInfo> CrunchyrollDownloader { get; }

        private Browser Browser { get; }

        private void FilterExistingEpisodes(string outputDirectory, List<IEpisodeInfo> episodes)
        {
            const string regex = @"S(?<season>\d+)E(?<episode>\d+) -";

            foreach (var episodeFile in Directory.GetFiles(outputDirectory, "*.mkv"))
            {
                var episodeMatch = Regex.Match(episodeFile, regex);

                if (!episodeMatch.Success
                    || !int.TryParse(episodeMatch.Groups["episode"].Value, out var episode)
                    || !int.TryParse(episodeMatch.Groups["season"].Value, out var season)) continue;

                Logger.LogWarning(
                    "Skipping episode {@EpisodeNumber} from season {@SeasonNumber} due to existing file {@FilePath}",
                    episode, season, episodeFile);
                episodes.RemoveAll(i => i.SeasonInfo.Season == season && i.SequenceNumber == episode);
            }
        }

        private bool IsValidSeriesUrl()
        {
            if (Uri.TryCreate(SeriesUrl, UriKind.Absolute, out var parsedUri))
            {
                var crunchyHost = parsedUri.Host.EndsWith("crunchyroll.com", StringComparison.InvariantCultureIgnoreCase);
                var rightSegmentsCount = parsedUri.Segments.Length >= 2;
                return crunchyHost && rightSegmentsCount;
            }

            return true;
        }

        public async ValueTask ExecuteAsync(IConsole console)
        {
            var isValidSeriesUrl = IsValidSeriesUrl();

            if (!isValidSeriesUrl)
            {
                throw new CommandException("The URL provided doesnt seem to be a crunchyroll SERIES page URL.", 1);
            }

            using var cookieFile = await CreateCookiesFile();

            var seriesInfo = await CrunchyrollSeasonProvider.GetSeries(SeriesUrl);

            var episodes = seriesInfo.Seasons
                .SelectMany(i => i.Episodes)
                .OrderBy(i => i.SeasonInfo.Season)
                .ThenBy(i => i.Number)
                .ToList();
            
            await Browser.DisposeAsync();

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
            
            //if (SkipExistingEpisodes) FilterExistingEpisodes(downloadParameters.OutputDirectory, episodes);
            
            if (!episodes.Any())
            {
                Logger.LogWarning("No episodes found");
            }
            else
            {
                var downloadParameters = await CreateDownloadParameters(cookieFile, seriesInfo);
                var downloadedFiles = await CrunchyrollDownloader.DownloadEpisodes(episodes, downloadParameters).ToArrayAsync();
                
                foreach (var downloadedFile in downloadedFiles)
                {
                    Logger.LogDebug("File downloaded to path: {@FilePath} {@Type}", downloadedFile.Path, downloadedFile.Type);
                }
            }
            
            if (cookieFile != null)
            {
                Logger.LogDebug("Cleaning cookie file {@CookieFile}", cookieFile);
                cookieFile?.Dispose();
            }

            Logger.LogInformation("Completed");
        }

        private async Task<DownloadParameters> CreateDownloadParameters(TemporaryCookieFile file, ISeriesInfo seriesInfo)
        {
            var isNvidiaAvailable = GpuAcceleration && await FfmpegService.IsNvidiaAvailable();

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
                UseNvidiaAcceleration = isNvidiaAvailable,
                UseHardwareAcceleration = HardwareAcceleration,
                ConversionPreset = ConversionPreset,
                DeleteTemporaryFiles = CleanTemporaryFiles,
                UseHevc = ConvertToHevc,
                TemporaryDirectory = TemporaryDirectory,
                ParallelDownloads = ParallelDownloadPoolSize,
                ParallelMerging = EpisodeBatchSize
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

            if (int.TryParse(range, out var episode))
            {
                return new[] { episode, episode };
            }

            throw new InvalidOperationException($"Invalid episode range. {range}");
        }
    }
}