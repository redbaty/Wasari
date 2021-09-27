using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.EventStream;
using CrunchyDownloader.Exceptions;
using CrunchyDownloader.Extensions;
using CrunchyDownloader.Models;
using Microsoft.Extensions.Logging;

namespace CrunchyDownloader.App
{
    internal class YoutubeDlService
    {
        public YoutubeDlService(ILogger<YoutubeDlService> logger, FfmpegService ffmpegService,
            DownloadProgressManager downloadProgressManager)
        {
            Logger = logger;
            FfmpegService = ffmpegService;
            DownloadProgressManager = downloadProgressManager;
        }

        private ILogger<YoutubeDlService> Logger { get; }

        private FfmpegService FfmpegService { get; }

        private DownloadProgressManager DownloadProgressManager { get; }
        
        [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
        public async Task DownloadEpisode(EpisodeInfo episodeInfo, DownloadParameters downloadParameters)
        {
            if (downloadParameters.CookieFilePath != null && !File.Exists(downloadParameters.CookieFilePath))
            {
                throw new CookieFileNotFoundException(downloadParameters.CookieFilePath);
            }

            var fileSafeName = new SanitizedFileName(episodeInfo.Name, string.Empty);

            var temporaryEpisodeFile = Path.Combine(downloadParameters.TemporaryDirectory,
                $"S{episodeInfo.SeasonInfo.Season:00}E{episodeInfo.Number:00} - {fileSafeName}_temp.mkv");

            var episodeFile = Path.Combine(downloadParameters.OutputDirectory,
                $"S{episodeInfo.SeasonInfo.Season:00}E{episodeInfo.Number:00} - {fileSafeName}.mkv");

            var outputDirectory = new DirectoryInfo(Path.GetDirectoryName(episodeFile) ??
                                                    throw new InvalidOperationException("Invalid output directory"));

            if (!outputDirectory.Exists)
                outputDirectory.Create();

            Logger.LogInformation("Starting download of episode {@Episode} of {@Season}...", episodeInfo.Name,
                $"Season {episodeInfo.SeasonInfo?.Season}");

            var progressBar = DownloadProgressManager.CreateProgressTracker();

            var arguments = new[]
            {
                "--encoding UTF-8",
                "--force-overwrites",
                "--newline",
                "--no-continue",
                "--no-part",
                string.IsNullOrEmpty(downloadParameters.CookieFilePath)
                    ? null
                    : $"--cookies \"{downloadParameters.CookieFilePath}\"",
                downloadParameters.Subtitles ? "--all-subs" : null,
                $"--user-agent \"{downloadParameters.UserAgent}\"",
                $"\"{episodeInfo.Url}\"",
                $"-o \"{temporaryEpisodeFile}\""
            }.Where(i => !string.IsNullOrEmpty(i));

            var files = new List<DownloadedFile>();

            var command = Cli.Wrap("yt-dlp")
                .WithArguments(arguments, false)
                .WithRetryCount(10)
                .WithLogger(Logger)
                .WithCommandHandler(@event =>
                {
                    if (@event is StandardOutputCommandEvent standardOutputCommandEvent)
                    {
                        if (standardOutputCommandEvent.Text.StartsWith("[info] Writing video subtitles to:"))
                        {
                            var path = Regex.Match(standardOutputCommandEvent.Text, @"[A-Z]\:\\.*").Value;
                            files.Add(new DownloadedFile
                            {
                                Type = FileType.Subtitle,
                                Path = path
                            });
                        }
                        else if (standardOutputCommandEvent.Text.StartsWith("[download] Destination:"))
                        {
                            var path = Regex.Match(standardOutputCommandEvent.Text, @"[A-Z]\:\\.*").Value;
                            var extension = Path.GetExtension(path);

                            files.Add(new DownloadedFile
                            {
                                Type = extension == ".ass" ? FileType.Subtitle : FileType.VideoFile,
                                Path = path
                            });
                        }
                        else if (standardOutputCommandEvent.Text.StartsWith("[download]") &&
                                 standardOutputCommandEvent.Text.Contains("%"))
                        {
                            if (standardOutputCommandEvent.Text.GetValueFromRegex<double>(@"(\d+\.\d+)%",
                                    out var parsedPercentage) &&
                                standardOutputCommandEvent.Text.GetValueFromRegex<string>(@"at (\d+\.\d+MiB/s)",
                                    out var speed))
                            {
                                var currentFile = files.Last();
                                progressBar?.Refresh((int)parsedPercentage,
                                    $"[YT-DLP][{currentFile.Type}]({speed}) {Path.GetFileName(currentFile.Path)}");
                            }
                        }
                    }
                });
            await command.Execute();

            files = files.GroupBy(i => new { i.Path, i.Type })
                .Select(i => i.First())
                .ToList();

            if (downloadParameters.Subtitles || downloadParameters.UseHevc)
            {
                var episode = files.Single(i => i.Type == FileType.VideoFile);
                var subFiles = files
                    .Where(i => i.Type == FileType.Subtitle && downloadParameters.Subtitles &&
                                (string.IsNullOrEmpty(downloadParameters.SubtitleLanguage) ||
                                 i.Path.EndsWith($"{downloadParameters.SubtitleLanguage}.ass",
                                     StringComparison.InvariantCultureIgnoreCase)))
                    .Select(i => i.Path)
                    .Where(File.Exists)
                    .ToArray();

                if (downloadParameters.DeleteTemporaryFiles)
                {
                    foreach (var unusedSub in files
                        .Where(i => i.Type == FileType.Subtitle)
                        .Select(i => i.Path)
                        .Except(subFiles)
                        .Where(File.Exists)) File.Delete(unusedSub);
                }

                if (subFiles.Any() || downloadParameters.UseHevc)
                {
                    await FfmpegService.MergeSubsToVideo(episode.Path, subFiles, episodeFile, downloadParameters,
                        progressBar);

                    if (downloadParameters.DeleteTemporaryFiles)
                    {
                        foreach (var temporaryFile in files.Select(i => i.Path).Where(File.Exists))
                        {
                            File.Delete(temporaryFile);
                        }
                    }
                }
                else if (downloadParameters.Subtitles && !subFiles.Any())
                {
                    Logger.LogWarning("Subtitle not found!");
                }
            }
            else
            {
                File.Move(temporaryEpisodeFile, episodeFile);
            }

            progressBar?.Refresh(progressBar.Max, $"[DONE] {Path.GetFileName(episodeFile)}");
        }
    }
}