using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using CrunchyDownloader.App;
using CrunchyDownloader.Exceptions;
using CrunchyDownloader.Models;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;

namespace CrunchyDownloader.Commands
{
    [Command("series")]
    internal class DownloadSeriesCommand : CrunchyAuthenticatedCommand, ICommand
    {
        public DownloadSeriesCommand(YoutubeDlService youtubeDlService, CrunchyRollService crunchyRollService,
            CrunchyRollAuthenticationService crunchyRollAuthenticationService, Browser browser,
            ILogger<DownloadSeriesCommand> logger) : base(
            crunchyRollAuthenticationService)
        {
            YoutubeDlService = youtubeDlService;
            CrunchyRollService = crunchyRollService;
            Browser = browser;
            Logger = logger;
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
        public string TemporaryDirectory { get; init; } = Path.GetTempPath();

        private YoutubeDlService YoutubeDlService { get; }

        private CrunchyRollService CrunchyRollService { get; }

        private ILogger<DownloadSeriesCommand> Logger { get; }

        private Browser Browser { get; }

        public async ValueTask ExecuteAsync(IConsole console)
        {
            using var cookieFile = await CreateCookiesFile();

            var episodes = await CrunchyRollService
                .GetEpisodes(SeriesUrl)
                .Where(i => i.SeasonInfo?.Title == null ||
                            !i.SeasonInfo.Title.Contains("Dub", StringComparison.InvariantCultureIgnoreCase))
                .OrderBy(i => i.SeasonInfo.Season)
                .ThenBy(i => i.Number)
                .ToArrayAsync();
            var userAgent = await Browser.GetUserAgentAsync();
            await Browser.DisposeAsync();

            var episodeRange = ParseRange(EpisodeRange, episodes.Length);
            var seasonsRange = ParseRange(SeasonsRange, episodes.Select(i => i.SeasonInfo.Season).Max());

            Logger.LogInformation("Episodes range is {@Range}", episodeRange);
            Logger.LogInformation("Seasons range is {@Range}", seasonsRange);

            episodes = episodes.Where(i =>
                i.Number >= episodeRange[0]
                && i.Number <= episodeRange[1]
                && i.SeasonInfo.Season >= seasonsRange[0]
                && i.SeasonInfo.Season <= seasonsRange[1]).ToArray();

            var downloadParameters = await CreateDownloadParameters(cookieFile, userAgent);
            var taskPool = new List<Task>(EpisodeBatchSize);

            foreach (var episode in episodes)
            {
                if (taskPool.Count >= EpisodeBatchSize)
                {
                    var taskEnded = await Task.WhenAny(taskPool);
                    taskPool.Remove(taskEnded);
                }

                taskPool.Add(YoutubeDlService.DownloadEpisode(episode, downloadParameters));
            }

            await Task.WhenAll(taskPool);

            if (cookieFile != null)
            {
                Logger.LogDebug("Cleaning cookie file {@CookieFile}", cookieFile);
                cookieFile?.Dispose();
            }

            Logger.LogInformation("Completed");
        }

        private async Task<DownloadParameters> CreateDownloadParameters(TemporaryCookieFile cookieFile,
            string userAgent)
        {
            var isNvidiaAvailable = GpuAcceleration && await FfmpegService.IsNvidiaAvailable();

            if (isNvidiaAvailable) Logger.LogInformation("NVIDIA hardware acceleration is available");

            return new DownloadParameters
            {
                CookieFilePath = cookieFile?.Path,
                CreateSubdirectory = CreateSubdirectory,
                SubtitleLanguage = SubtitleLanguage,
                Subtitles = !string.IsNullOrEmpty(SubtitleLanguage) || Subtitles,
                OutputDirectory = OutputDirectory,
                UserAgent = userAgent,
                UseNvidiaAcceleration = isNvidiaAvailable,
                UseHardwareAcceleration = HardwareAcceleration,
                ConversionPreset = ConversionPreset,
                DeleteTemporaryFiles = CleanTemporaryFiles,
                UseX265 = ConvertToHevc,
                TemporaryDirectory = TemporaryDirectory
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