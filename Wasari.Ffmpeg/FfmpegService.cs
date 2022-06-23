using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.EventStream;
using Microsoft.Extensions.Logging;
using Wasari.Abstractions;
using Wasari.Abstractions.Extensions;
using WasariEnvironment;

namespace Wasari.Ffmpeg
{
    public class FfmpegService
    {
        public FfmpegService(ILogger<FfmpegService> logger, EnvironmentService environmentService, FfprobeService ffprobeService)
        {
            Logger = logger;
            EnvironmentService = environmentService;
            FfprobeService = ffprobeService;
            Ffmpeg = EnvironmentService.GetFeatureOrThrow(EnvironmentFeatureType.Ffmpeg);
        }

        private ILogger<FfmpegService> Logger { get; }

        private EnvironmentService EnvironmentService { get; }

        private EnvironmentFeature Ffmpeg { get; }
        
        private FfprobeService FfprobeService { get; }

        private static object CheckShaderLock { get; } = new();


        private async IAsyncEnumerable<string> GetAvailableHardwareAccelerationMethods()
        {
            var arguments = new[]
            {
                "-hide_banner -hwaccels"
            };

            var command = Cli.Wrap(Ffmpeg.Path)
                .WithValidation(CommandResultValidation.None)
                .WithArguments(arguments, false);

            await foreach (var commandEvent in command.ListenAsync())
            {
                if (commandEvent is StandardOutputCommandEvent standardOutputCommandEvent)
                {
                    yield return standardOutputCommandEvent.Text;
                }
            }
        }

        private async IAsyncEnumerable<string> GetAvailableEncoders()
        {
            var arguments = new[]
            {
                "-hide_banner -encoders"
            };

            var command = Cli.Wrap(Ffmpeg.Path)
                .WithValidation(CommandResultValidation.None)
                .WithArguments(arguments, false);

            await foreach (var commandEvent in command.ListenAsync())
            {
                if (commandEvent is StandardOutputCommandEvent standardOutputCommandEvent)
                {
                    yield return standardOutputCommandEvent.Text;
                }
            }
        }

        public async Task<bool> IsNvidiaAvailable() =>
            await GetAvailableHardwareAccelerationMethods()
                .AnyAsync(i => string.Equals(i, "cuda", StringComparison.InvariantCultureIgnoreCase))
            && await GetAvailableEncoders()
                .AnyAsync(i => i.Contains("hevc_nvenc", StringComparison.InvariantCultureIgnoreCase));

        private IEnumerable<string> CreateArguments(IReadOnlyCollection<EpisodeInfoVideoSource> videoFiles, IEnumerable<string> subtitlesFiles,
            string newVideoFile, DownloadParameters downloadParameters)
        {
            if (downloadParameters.UseAnime4K)
                yield return "-init_hw_device vulkan";

            foreach (var videoFile in videoFiles)
            {
                yield return $"-i \"{videoFile.LocalPath}\"";
            }

            var videoMappings = CreateVideoMappings(videoFiles).Aggregate((x, y) => $"{x} {y}");
            var videoMetadataMapping = CreateMetadataMappings(videoFiles).Aggregate((x, y) => $"{x} {y}");
            
            var subtitleArguments = CreateSubtitleArguments(subtitlesFiles, videoFiles.Count, videoMappings, videoMetadataMapping) ?? videoMappings;

            if (downloadParameters.UseAnime4K)
                yield return
                    "-filter_complex \"hwupload,libplacebo=w=3840:h=2160:custom_shader_path=main.glsl,hwdownload,format=yuv420p\"";

            if (!string.IsNullOrEmpty(subtitleArguments))
                yield return subtitleArguments;

            if (downloadParameters.UseHevc)
            {
                if (downloadParameters.UseNvidiaAcceleration)
                {
                    yield return "-c:v hevc_nvenc";

                    var useExtraSei =
                        EnvironmentService.GetModuleVersion(EnvironmentFeatureType.Ffmpeg, "libavcodec") is
                        {
                            Major: >= 59
                        };

                    if (useExtraSei)
                        yield return "-extra_sei 0";

                    yield return "-rc vbr -cq 24 -qmin 24 -qmax 24 -profile:v main10 -pix_fmt p010le";
                }
                else
                    yield return "-crf 24 -pix_fmt yuv420p10le -c:v libx265 -tune animation -x265-params profile=main10";
            }
            else
            {
                if (downloadParameters.UseAnime4K)
                {
                    if (downloadParameters.UseNvidiaAcceleration) yield return "-c:v h264_nvenc -rc vbr -cq 24 -qmin 24 -qmax 24";
                    else yield return "-c:v lix264";
                }
                else
                    yield return "-c:v copy";
            }

            if (!string.IsNullOrEmpty(downloadParameters.ConversionPreset))
            {
                yield return $"-preset {downloadParameters.ConversionPreset}";
            }

            yield return "-c:a copy";
            yield return "-scodec copy";
            yield return "-y";
            yield return $"\"{newVideoFile}\"";
        }

        private static IEnumerable<string> CreateVideoMappings(IReadOnlyCollection<EpisodeInfoVideoSource> videoFiles) => Enumerable.Range(0, videoFiles.Count).Select(index => $"-map {index}");

        private static IEnumerable<string> CreateMetadataMappings(IReadOnlyCollection<EpisodeInfoVideoSource> videoFiles) => videoFiles.Select((i, index) => $"-metadata:s:a:{index} language=\"{i.Language}\"");

        private static string CreateSubtitleArguments(IEnumerable<string> subs, int startingIndex, string videoMappings, string videoMetadataMapping)
        {
            var subtitlesFiles = subs?.OrderBy(i => i).ToArray();

            if (subtitlesFiles is not { Length: > 0 })
                return null;

            var aggregate = subtitlesFiles.Select(i => $"-f ass -i \"{i}\"")
                .Aggregate((x, y) => $"{x} {y}");

            var mappings = subtitlesFiles.Select((_, i) => $"-map {i + startingIndex}")
                .Aggregate((x, y) => $"{x} {y}");

            var metadata = subtitlesFiles.Select((i, index) =>
                    $"-metadata:s:s:{index} language={i.Split(".").Reverse().Skip(1).First()}")
                .ToArray();

            var metadataMappings = metadata.Aggregate((x, y) => $"{x} {y}");

            return $"{aggregate} {videoMappings} {mappings} {metadataMappings} {videoMetadataMapping}";
        }

        public async Task Encode(string episodeId, EpisodeInfoVideoSource[] videoFiles, string[] subtitlesFiles,
            string newVideoFile, DownloadParameters downloadParameters)
        {
            Logger.LogInformation("Encoding of episode {@Episode} started", episodeId);

            if (downloadParameters.UseAnime4K)
            {
                lock (CheckShaderLock)
                {
                    if (!File.Exists("main.glsl"))
                    {
                        Logger.LogInformation("Creating main shader");
                        using var manifestResourceStream =
                            Assembly.GetAssembly(typeof(FfmpegService))!.GetManifestResourceStream(
                                "Wasari.Ffmpeg.shaders.main.glsl");
                        using var fs = File.Create("main.glsl");
                        manifestResourceStream!.CopyTo(fs);
                        fs.Close();
                        manifestResourceStream.Close();
                    }
                }
            }
            
            var mediaAnalysis = await videoFiles.ToAsyncEnumerable()
                .SelectAwait(async o => await FfprobeService.GetVideoDuration(o.LocalPath))
                .Distinct()
                .MaxAsync();

            var statusUpdateEnabled = mediaAnalysis.HasValue;

            if (!statusUpdateEnabled)
            {
                Logger.LogWarning("Failed to determined video duration for files {@Files} {@DownloadParameters}", videoFiles.Select(i => i.LocalPath).ToArray(), downloadParameters);
            }

            if (statusUpdateEnabled)
            {
                var update = new ProgressUpdate
                {
                    Title = "[FFMPEG] Merge Video To Subtitles",
                    Type = ProgressUpdateTypes.Current,
                    Value = 0,
                    EpisodeId = episodeId
                };

                Logger.LogProgressUpdate(update);

                update = new ProgressUpdate
                {
                    Title = "FFMPEG - Merge Video To Subtitles",
                    Type = ProgressUpdateTypes.Max,
                    Value = (int)mediaAnalysis.Value.TotalSeconds,
                    EpisodeId = episodeId
                };

                Logger.LogProgressUpdate(update);
            }

            var temporaryFinalFile = $"{Path.GetTempFileName()}{Path.GetExtension(newVideoFile)}";

            var command = Cli.Wrap(Ffmpeg.Path)
                .WithArguments(CreateArguments(videoFiles, subtitlesFiles, temporaryFinalFile, downloadParameters)
                    .Where(i => !string.IsNullOrEmpty(i)), false);

            Logger.LogInformation("Encoding final video file. {@Command}", command.ToString());

            var stopwatch = Stopwatch.StartNew();

            await foreach (var commandEvent in command.ListenAsync())
            {
                var text = commandEvent switch
                {
                    StandardErrorCommandEvent standardErrorCommandEvent => standardErrorCommandEvent.Text,
                    StandardOutputCommandEvent standardOutputCommandEvent => standardOutputCommandEvent.Text,
                    _ => null
                };

                if (text != null
                    && text.GetValueFromRegex<double>(@"speed=(\d+.\d+)x", out var speed)
                    && text.GetValueFromRegex<string>(@"time=(\d+:\d+:\d+.\d+)", out var time)
                    && time != null)
                {
                    var timespan = TimeSpan.Parse(time);

                    if (statusUpdateEnabled)
                    {
                        var update = new ProgressUpdate
                        {
                            Title = $"[FFMPEG]({speed:0.000}x) {Path.GetFileName(newVideoFile)}",
                            Type = ProgressUpdateTypes.Current,
                            Value = (int)timespan.TotalSeconds,
                            EpisodeId = episodeId
                        };

                        Logger.LogProgressUpdate(update);
                    }
                }

                Logger.LogTrace("[FFMpeg] {@Text}", text);
            }

            if (downloadParameters.DeleteTemporaryFiles)
            {
                var deletedFiles = 0;

                foreach (var videoFile in videoFiles)
                {
                    if (File.Exists(videoFile.LocalPath))
                    {
                        File.Delete(videoFile.LocalPath);
                        deletedFiles++;
                    }
                }

                if (subtitlesFiles != null)
                {
                    foreach (var subtitleFile in subtitlesFiles.Where(File.Exists))
                    {
                        File.Delete(subtitleFile);
                        deletedFiles++;
                    }
                }

                Logger.LogInformation("{@DeletedFiles} were cleaned. {@EpisodeId}", deletedFiles, episodeId);
            }

            File.Move(temporaryFinalFile, newVideoFile);
            stopwatch.Stop();

            Logger.LogInformation("Encoding of {@Episode} to {@NewVideoFile} has ended and took {@Elapsed}", episodeId,
                newVideoFile, stopwatch.Elapsed);
        }
    }
}