using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.EventStream;
using FFMpegCore;
using Microsoft.Extensions.Logging;
using Wasari.Abstractions;
using Wasari.Abstractions.Extensions;
using WasariEnvironment;

namespace Wasari.Ffmpeg
{
    public class FfmpegService
    {
        public FfmpegService(ILogger<FfmpegService> logger, EnvironmentService environmentService)
        {
            Logger = logger;
            EnvironmentService = environmentService;
            Ffmpeg = EnvironmentService.GetFeatureOrThrow(EnvironmentFeatureType.Ffmpeg);
        }

        private ILogger<FfmpegService> Logger { get; }

        private EnvironmentService EnvironmentService { get; }
        
        private EnvironmentFeature Ffmpeg { get; }

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

        private IEnumerable<string> CreateArguments(string videoFile, IEnumerable<string> subtitlesFiles,
            string newVideoFile, DownloadParameters downloadParameters)
        {
            if (downloadParameters.UseAnime4K)
                yield return "-init_hw_device vulkan";

            yield return $"-i \"{videoFile}\"";
            var subtitleArguments = CreateSubtitleArguments(subtitlesFiles);

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
                    yield return "-pix_fmt yuv420p10le -c:v libx265 -tune animation -x265-params profile=main10";
            }
            else
            {
                if (downloadParameters.UseAnime4K)
                {
                    if (downloadParameters.UseNvidiaAcceleration) yield return "-c:v h264_nvenc";
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

        private static string CreateSubtitleArguments(IEnumerable<string> subs)
        {
            var subtitlesFiles = subs?.OrderBy(i => i).ToArray();

            if (subtitlesFiles is not { Length: > 0 })
                return null;

            var aggregate = subtitlesFiles.Select(i => $"-f ass -i \"{i}\"")
                .Aggregate((x, y) => $"{x} {y}");

            var mappings = subtitlesFiles.Select((_, i) => $"-map {i + 1}")
                .Aggregate((x, y) => $"{x} {y}");

            var metadata = subtitlesFiles.Select((i, index) =>
                    $"-metadata:s:s:{index} language={i.Split(".").Reverse().Skip(1).First()}")
                .ToArray();

            var metadataMappings = metadata.Aggregate((x, y) => $"{x} {y}");

            return $"{aggregate} -map 0:v -map 0:a {mappings} {metadataMappings}";
        }

        public async Task Encode(string episodeId, string videoFile, IEnumerable<string> subtitlesFiles,
            string newVideoFile, DownloadParameters downloadParameters)
        {
            var update = new ProgressUpdate
            {
                Title = "[FFMPEG] Merge Video To Subtitles",
                Type = ProgressUpdateTypes.Current,
                Value = 0,
                EpisodeId = episodeId
            };

            Logger.LogProgressUpdate(update);
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
                    }
                }
            }

            var mediaAnalysis = await FFProbe.AnalyseAsync(videoFile);

            update = new ProgressUpdate
            {
                Title = "FFMPEG - Merge Video To Subtitles",
                Type = ProgressUpdateTypes.Max,
                Value = (int)mediaAnalysis.Duration.TotalSeconds,
                EpisodeId = episodeId
            };

            Logger.LogProgressUpdate(update);

            var command = Cli.Wrap(Ffmpeg.Path)
                .WithArguments(CreateArguments(videoFile, subtitlesFiles, newVideoFile, downloadParameters)
                    .Where(i => !string.IsNullOrEmpty(i)), false);

            Logger.LogDebug("Merging video file with subtitles. {@Command}", command.ToString());

            var stopwatch = Stopwatch.StartNew();

            await foreach (var commandEvent in command.ListenAsync())
            {
                var text = commandEvent switch
                {
                    StandardErrorCommandEvent standardErrorCommandEvent => standardErrorCommandEvent.Text,
                    StandardOutputCommandEvent standardOutputCommandEvent => standardOutputCommandEvent.Text,
                    _ => null
                };

                if (text.GetValueFromRegex<double>(@"speed=(\d+.\d+)x", out var speed) &&
                    text.GetValueFromRegex<string>(@"time=(\d+:\d+:\d+.\d+)", out var time))
                {
                    var timespan = TimeSpan.Parse(time);

                    update = new ProgressUpdate
                    {
                        Title = $"[FFMPEG]({speed:0.000}x) {Path.GetFileName(newVideoFile)}",
                        Type = ProgressUpdateTypes.Current,
                        Value = (int)timespan.TotalSeconds,
                        EpisodeId = episodeId
                    };

                    Logger.LogProgressUpdate(update);
                }

                Logger?.LogTrace("[FFMpeg] {@Text}", text);
            }

            stopwatch.Stop();

            Logger.LogInformation("Encoding of {@Episode} to {@NewVideoFile} has ended and took {@Elapsed}", episodeId,
                newVideoFile, stopwatch.Elapsed);
        }
    }
}