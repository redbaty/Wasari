using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.EventStream;
using Microsoft.Extensions.Logging;
using Wasari.Abstractions;
using Wasari.Abstractions.Extensions;
using Wasari.Crunchyroll.Abstractions;
using Wasari.Crunchyroll.API;
using Wasari.Crunchyroll.Extensions;
using WasariEnvironment;

namespace Wasari.Crunchyroll
{
    internal class YoutubeDlService
    {
        public YoutubeDlService(ILogger<YoutubeDlService> logger, CrunchyrollApiServiceFactory crunchyrollApiServiceFactory, EnvironmentService environmentService)
        {
            Logger = logger;
            CrunchyrollApiServiceFactory = crunchyrollApiServiceFactory;
            YtDlp = environmentService.GetFeatureOrThrow(EnvironmentFeatureType.YtDlp);
        }

        private ILogger<YoutubeDlService> Logger { get; }

        private CrunchyrollApiServiceFactory CrunchyrollApiServiceFactory { get; }

        private EnvironmentFeature YtDlp { get; }

        private static async IAsyncEnumerable<DownloadedFile> DownloadSubs(string episodeId,
            IEnumerable<ApiEpisodeStreamSubtitle> subtitles, DownloadParameters downloadParameters)
        {
            using var httpClient = new HttpClient();

            foreach (var subtitle in subtitles)
            {
                using var respostaHttp = await httpClient.GetAsync(subtitle.Url);
                await using var remoteStream = await respostaHttp.Content.ReadAsStreamAsync();
                var temporaryFile = Path.Combine(downloadParameters.TemporaryDirectory ?? Path.GetTempPath(), $"{episodeId}.{subtitle.Locale}.{subtitle.Format}");
                await using var fs = File.Create(temporaryFile);
                await remoteStream.CopyToAsync(fs);

                yield return new SubtitleFile
                {
                    Type = FileType.Subtitle,
                    Path = temporaryFile,
                    Language = subtitle.Locale.Replace("-", string.Empty)
                };
            }
        }

        private async Task<BetaEpisodeResult> GetUrlAndSubtitles(CrunchyrollEpisodeInfo episodeInfo,
            DownloadParameters downloadParameters)
        {
            var crunchyrollApiService = CrunchyrollApiServiceFactory.GetService();
            var streams = await crunchyrollApiService.GetStreams(episodeInfo.Url);
            var preferedLink = streams.Streams
                .SingleOrDefault(i => i.Type == "adaptive_hls" && string.IsNullOrEmpty(i.Locale));

            Logger.LogInformation("Stream URL found for Episode {@Episode}: {@Url}", episodeInfo.FilePrefix, preferedLink);

            if (preferedLink == null)
                throw new InvalidOperationException("Could not determine stream link");

            return new BetaEpisodeResult
            {
                Files = await DownloadSubs(episodeInfo.Id, streams.Subtitles, downloadParameters).ToArrayAsync(),
                Url = preferedLink.Url
            };
        }

        [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
        public async Task<YoutubeDlResult> DownloadEpisode(CrunchyrollEpisodeInfo episodeInfo,
            DownloadParameters downloadParameters)
        {
            var url = episodeInfo.Url;
            var files = new List<DownloadedFile>();

            if (downloadParameters.CookieFilePath != null && !File.Exists(downloadParameters.CookieFilePath))
            {
                throw new CookieFileNotFoundException(downloadParameters.CookieFilePath);
            }

            if (episodeInfo.Url.EndsWith("/streams"))
            {
                var downloadBetaEpisode = await GetUrlAndSubtitles(episodeInfo, downloadParameters);
                url = downloadBetaEpisode.Url;
                files.AddRange(downloadBetaEpisode.Files);
            }

            var fileSafeName = episodeInfo.Name.AsSafePath();

            var temporaryEpisodeFile = Path.Combine(downloadParameters.TemporaryDirectory ?? Directory.GetCurrentDirectory(),
                $"{episodeInfo.FilePrefix} - {fileSafeName}_temp.mp4");

            Logger.LogInformation("Download of episode {@Episode} started", episodeInfo.FilePrefix);

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
                $"-f \"{downloadParameters.Format}\"",
                $"\"{url}\"",
                $"-o \"{temporaryEpisodeFile}\""
            }.Where(i => !string.IsNullOrEmpty(i));

            var command = Cli.Wrap(YtDlp.Path)
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

                            files.Add(extension == ".ass"
                                ? new SubtitleFile
                                {
                                    Language = Regex.Match(path, "\\.(?<lang>(.*))\\.ass").Groups["lang"].Value.Replace("-", string.Empty),
                                    Path = path
                                }
                                : new DownloadedFile
                                {
                                    Type = FileType.VideoFile,
                                    Path = path
                                });
                        }
                        else if (standardOutputCommandEvent.Text.StartsWith("[download]") &&
                                 standardOutputCommandEvent.Text.Contains('%'))
                        {
                            if (standardOutputCommandEvent.Text.GetValueFromRegex<double>(@"(\d+\.\d+)%",
                                    out var parsedPercentage) &&
                                standardOutputCommandEvent.Text.GetValueFromRegex<string>(@"at (\d+\.\d+\w+/s)",
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

            var filesNotFound = files.Where(i => !File.Exists(i.Path)).Select(i => new FileNotFoundException(i.Path)).ToArray();
            if (filesNotFound.Any())
            {
                throw new AggregateException("Invalid download(s) destination parsed from yt-dlp.", filesNotFound.Cast<Exception>());
            }

            if (!string.IsNullOrEmpty(downloadParameters.SubtitleLanguage))
            {
                var subtitleFiles = files
                    .OfType<SubtitleFile>()
                    .Where(i => i.Type == FileType.Subtitle && string.Equals(i.Language, downloadParameters.SubtitleLanguage, StringComparison.InvariantCultureIgnoreCase))
                    .ToArray();

                if (subtitleFiles.Length == 0)
                    throw new Exception("No subtitles found for selected language");

                foreach (var file in files.Where(i => i is SubtitleFile subtitleFile && !string.Equals(subtitleFile.Language, downloadParameters.SubtitleLanguage, StringComparison.InvariantCultureIgnoreCase) && i.Path != null))
                {
                    if (File.Exists(file.Path!))
                        File.Delete(file.Path!);
                    
                    files.Remove(file);
                }
            }

            Logger.LogInformation("Download of episode {@Episode} ended", $"{episodeInfo.FilePrefix}");

            return new YoutubeDlResult
            {
                Files = files,
                Episode = episodeInfo
            };
        }
    }
}