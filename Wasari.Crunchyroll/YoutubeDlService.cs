using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.EventStream;
using Microsoft.Extensions.Logging;
using Wasari.Abstractions;
using Wasari.Abstractions.Extensions;
using Wasari.Crunchyroll.Abstractions;
using Wasari.Crunchyroll.Extensions;

namespace Wasari.Crunchyroll
{
    internal class YoutubeDlService
    {
        public YoutubeDlService(ILogger<YoutubeDlService> logger)
        {
            Logger = logger;
        }

        private ILogger<YoutubeDlService> Logger { get; }

        [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
        public async Task<YoutubeDlResult> DownloadEpisode(CrunchyrollEpisodeInfo episodeInfo,
            DownloadParameters downloadParameters)
        {
            if (downloadParameters.CookieFilePath != null && !File.Exists(downloadParameters.CookieFilePath))
            {
                throw new CookieFileNotFoundException(downloadParameters.CookieFilePath);
            }

            var fileSafeName = episodeInfo.Name.AsSafePath();

            var temporaryEpisodeFile = Path.Combine(downloadParameters.TemporaryDirectory,
                $"{episodeInfo.FilePrefix} - {fileSafeName}_temp.mkv");

            Logger.LogInformation("Starting download of episode {@Episode} of {@Season}...", episodeInfo.Name,
                $"Season {episodeInfo.SeasonInfo?.Season}");

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
                            var path = standardOutputCommandEvent.Text[35..].Trim();

                            files.Add(new DownloadedFile
                            {
                                Type = FileType.Subtitle,
                                Path = path
                            });
                        }
                        else if (standardOutputCommandEvent.Text.StartsWith("[download] Destination:"))
                        {
                            var path = standardOutputCommandEvent.Text[24..].Trim();
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

                                var update = new ProgressUpdate
                                {
                                    Title =
                                        $"[YT-DLP][{currentFile.Type}]({speed}) {Path.GetFileName(currentFile.Path)}",
                                    Type = ProgressUpdateTypes.Current,
                                    Value = (int)parsedPercentage,
                                    EpisodeId = episodeInfo.FilePrefix
                                };

                                Logger.LogProgressUpdate(update);
                            }
                        }
                    }
                });
            await command.Execute();

            files = files.GroupBy(i => new { i.Path, i.Type })
                .Select(i => i.First())
                .ToList();

            var filesNotFound = files.Where(i => File.Exists(i.Path)).Select(i => new FileNotFoundException(i.Path)).ToArray();
            if (filesNotFound.Any())
            {
                throw new AggregateException($"Invalid download(s) destination parsed from yt-dlp.", filesNotFound.Cast<Exception>());
            }

            return new YoutubeDlResult
            {
                Files = files,
                Episode = episodeInfo
            };
        }
    }
}