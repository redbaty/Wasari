using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.EventStream;
using Microsoft.Extensions.Logging;
using Wasari.Abstractions;
using Wasari.Abstractions.Extensions;
using Wasari.CliWrap.Extensions;
using WasariEnvironment;

namespace Wasari.YoutubeDl
{
    public class YoutubeDlService
    {
        public YoutubeDlService(ILogger<YoutubeDlService> logger, EnvironmentService environmentService)
        {
            Logger = logger;
            YtDlp = environmentService.GetFeatureOrThrow(EnvironmentFeatureType.YtDlp);
        }

        private ILogger<YoutubeDlService> Logger { get; }

        private EnvironmentFeature YtDlp { get; }


        [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
        public async Task<YoutubeDlResult> DownloadEpisode(
            EpisodeInfoVideoSource videoSource,
            DownloadParameters downloadParameters)
        {
            var episodeInfo = videoSource.Episode;

            if (episodeInfo == null)
            {
                throw new ArgumentNullException(nameof(episodeInfo));
            }
            
            var url = videoSource.Url;
            var files = new List<DownloadedFile>();
            
            var fileSafeName = episodeInfo.Name.AsSafePath();

            var temporaryEpisodeFile = Path.Combine(downloadParameters.TemporaryDirectory ?? Directory.GetCurrentDirectory(),
                $"{episodeInfo.FilePrefix} - {fileSafeName}_{episodeInfo.Id}_temp.mp4");

            Logger.LogInformation("Download of episode {@Episode} started \"{@Url}\"", episodeInfo.FilePrefix, videoSource.Url);

            var arguments = new[]
            {
                "--encoding UTF-8",
                "--force-overwrites",
                "--newline",
                "--no-continue",
                "--no-part",
                downloadParameters.Subtitles ? "--all-subs" : null,
                $"-f \"{downloadParameters.Format}\"",
                $"\"{url}\"",
                $"-o \"{temporaryEpisodeFile}\""
            }.Where(i => !string.IsNullOrEmpty(i));

            var command = Cli.Wrap(YtDlp.Path)
                .WithArguments(arguments!, false)
                .WithRetryCount(10)
                .WithLogger(Logger)
                .WithCommandHandler(@event => ProcessCommandEvent(@event, files, episodeInfo));
            await command.Execute();

            files = files.GroupBy(i => new { i.Path, i.Type })
                .Select(i => i.First())
                .ToList();

            var filesNotFound = files.Where(i => !File.Exists(i.Path)).Select(i => new FileNotFoundException(i.Path)).ToArray();
            if (filesNotFound.Any())
            {
                throw new AggregateException("Invalid download(s) destination parsed from yt-dlp.", filesNotFound.Cast<Exception>());
            }

            if (downloadParameters.SubtitleLanguage is { Length: > 0 })
            {
                var subtitlesCount = files
                    .OfType<SubtitleFile>()
                    .Count(i => i.Type == FileType.Subtitle && downloadParameters.SubtitleLanguage.Contains(i.Language));

                if (subtitlesCount == 0)
                    throw new Exception("No subtitles found for selected language");

                foreach (var file in files.Where(i => i is SubtitleFile subtitleFile && !downloadParameters.SubtitleLanguage.Contains(subtitleFile.Language) && i.Path != null).ToArray())
                {
                    if (File.Exists(file.Path!))
                        File.Delete(file.Path!);

                    files.Remove(file);
                }
            }

            var currentFile = files.Single(i => i.Type == FileType.VideoFile);
            Logger.LogInformation("Download of episode {@Episode} ended", $"{episodeInfo.FilePrefix}");
            Logger.LogProgressUpdate(CreateProgressUpdate(100, ProgressUpdateTypes.Completed, $"[YT-DLP][{currentFile.Type}][DONE] {Path.GetFileName(currentFile.Path)}", $"{episodeInfo.Id}_{currentFile.Type}"));

            videoSource.LocalPath = temporaryEpisodeFile;
            return new YoutubeDlResult
            {
                Files = files,
                Episode = episodeInfo,
                Source = videoSource
            };
        }

        private void ProcessCommandEvent(CommandEvent @event, List<DownloadedFile> files, IEpisodeInfo episodeInfo)
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
                            Language = Regex.Match(path, "\\.(?<lang>(.*))\\.ass").Groups["lang"].Value.Replace("-", string.Empty).ToLower(),
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

                        Logger.LogProgressUpdate(CreateProgressUpdate(parsedPercentage, ProgressUpdateTypes.Current, $"[YT-DLP][{currentFile.Type}]({speed}) {Path.GetFileName(currentFile.Path)}", $"{episodeInfo.Id}_{currentFile.Type}"));
                    }
                }
            }
        }

        private static ProgressUpdate CreateProgressUpdate(double parsedPercentage, ProgressUpdateTypes updateType, string message, string id)
        {
            return new ProgressUpdate
            {
                Title = message,
                Type = updateType,
                Value = (int)parsedPercentage,
                EpisodeId = id
            };
        }
    }
}