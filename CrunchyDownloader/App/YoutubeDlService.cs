using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.EventStream;
using CrunchyDownloader.Extensions;
using CrunchyDownloader.Models;
using Microsoft.Extensions.Logging;

namespace CrunchyDownloader.App
{
    public class YoutubeDlService
    {
        public YoutubeDlService(ILogger<YoutubeDlService> logger, FfmpegService ffmpegService)
        {
            Logger = logger;
            FfmpegService = ffmpegService;
        }

        private ILogger<YoutubeDlService> Logger { get; }

        private FfmpegService FfmpegService { get; }

        public async Task DownloadEpisode(EpisodeInfo episodeInfo, DownloadParameters downloadParameters)
        {
            if (!File.Exists(downloadParameters.CookieFilePath))
            {
                throw new Exception("Cookie file not found.");
            }

            var directory = downloadParameters.CreateSubdirectory
                ? Path.Combine(downloadParameters.OutputDirectory, episodeInfo.Series.Name)
                : downloadParameters.OutputDirectory;

            var temporaryEpisodeFile = Path.Combine(downloadParameters.TemporaryDirectory,
                $"S{episodeInfo.SeasonInfo.Season:00}E{episodeInfo.Number:00} - {episodeInfo.Name}_temp.mkv");
            
            var episodeFile = Path.Combine(directory,
                $"S{episodeInfo.SeasonInfo.Season:00}E{episodeInfo.Number:00} - {episodeInfo.Name}{(downloadParameters.Subtitles ? "_temp" : string.Empty)}.mkv");

            Logger.LogInformation("Starting download of episode {@Episode} of {@Season}...", episodeInfo.Name,
                $"Season {episodeInfo.SeasonInfo?.Season}");
            
            var arguments = new List<string>
            {
                "--encoding UTF-8",
                "--newline",
                "--no-continue",
                "--no-part",
                $"--cookies \"{downloadParameters.CookieFilePath}\"",
                $"--user-agent \"{downloadParameters.UserAgent}\"",
                $"\"{episodeInfo.Url}\"",
                $"-o \"{temporaryEpisodeFile}\""
            };

            if (downloadParameters.Subtitles)
            {
                arguments.Add("--all-subs");
            }

            var files = new List<DownloadedFile>();

            var command = Cli.Wrap("youtube-dl")
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

                        if (standardOutputCommandEvent.Text.StartsWith("[download] Destination:"))
                        {
                            var path = Regex.Match(standardOutputCommandEvent.Text, @"[A-Z]\:\\.*").Value;
                            files.Add(new DownloadedFile
                            {
                                Type = FileType.VideoFile,
                                Path = path
                            });
                        }
                    }
                });
            await command.Execute();

            if (downloadParameters.Subtitles)
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

                if (subFiles.Any())
                {
                    await FfmpegService.MergeSubsToVideo(episode.Path, subFiles, episodeFile, downloadParameters);

                    if (downloadParameters.DeleteTemporaryFiles)
                    {
                        foreach (var temporaryFile in files.Select(i => i.Path).Where(File.Exists))
                        {
                            File.Delete(temporaryFile);
                        }
                    }
                }
                else
                {
                    Logger.LogWarning("Subtitle not found!");
                }
            }
            else
            {
                File.Move(temporaryEpisodeFile, episodeFile);
            }
        }
    }
}