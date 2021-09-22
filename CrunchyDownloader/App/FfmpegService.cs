using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.EventStream;
using CrunchyDownloader.Extensions;
using CrunchyDownloader.Models;
using FFMpegCore;
using Konsole;
using Microsoft.Extensions.Logging;

namespace CrunchyDownloader.App
{
    public class FfmpegService
    {
        public FfmpegService(ILogger<FfmpegService> logger)
        {
            Logger = logger;
        }

        private ILogger<FfmpegService> Logger { get; }

        private static async IAsyncEnumerable<string> GetAvailableHardwareAccelerationMethods()
        {
            var arguments = new[]
            {
                "-hide_banner -hwaccels"
            };

            var command = Cli.Wrap("ffmpeg")
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

        private static async IAsyncEnumerable<string> GetAvailableEncoders()
        {
            var arguments = new[]
            {
                "-hide_banner -encoders"
            };

            var command = Cli.Wrap("ffmpeg")
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

        public static async Task<bool> IsNvidiaAvailable() =>
            await GetAvailableHardwareAccelerationMethods()
                .AnyAsync(i => string.Equals(i, "cuda", StringComparison.InvariantCultureIgnoreCase))
            && await GetAvailableEncoders()
                .AnyAsync(i => i.Contains("hevc_nvenc", StringComparison.InvariantCultureIgnoreCase));

        public async Task MergeSubsToVideo(string videoFile, string[] subtitlesFiles, string newVideoFile,
            DownloadParameters downloadParameters, ProgressBar progressBar)
        {
            progressBar.Refresh(0, "FFMPEG - Merge Video To Subtitles");

            subtitlesFiles = subtitlesFiles?.OrderBy(i => i).ToArray();

            var aggregate = subtitlesFiles?
                .Select(i => $"-f ass -i \"{i}\"")
                .Aggregate((x, y) => $"{x} {y}");

            var mappings = subtitlesFiles?
                .Select((s, i) => $"-map {i + 1}")
                .Aggregate((x, y) => $"{x} {y}");

            var metadata = subtitlesFiles?
                .Select((i, index) =>
                    $"-metadata:s:s:{index} language={i.Split(".").Reverse().Skip(1).First()}")
                .ToArray();

            var metadataMappings = metadata?
                .Aggregate((x, y) => $"{x} {y}");

            var subtitleArguments = subtitlesFiles != null && subtitlesFiles.Any()
                ? $"{aggregate} -map 0 {mappings} {metadataMappings}"
                : null;

            var arguments = new[]
                {
                    downloadParameters.UseHardwareAcceleration
                        ? $"-hwaccel {(downloadParameters.UseNvidiaAcceleration ? "cuda" : "auto")}"
                        : null,
                    $"-i \"{videoFile}\"",
                    $"{subtitleArguments}",
                    downloadParameters.UseX265
                        ? downloadParameters.UseNvidiaAcceleration ? "-c:v hevc_nvenc -pix_fmt p010le" :
                        "-pix_fmt yuv420p10le -c:v libx265 -x265-params profile=main10"
                        : "-c:v copy",
                    string.IsNullOrEmpty(downloadParameters.ConversionPreset)
                        ? null
                        : $"-preset {downloadParameters.ConversionPreset}",
                    "-c:a copy",
                    "-scodec copy",
                    "-y",
                    $"\"{newVideoFile}\""
                }
                .Where(i => !string.IsNullOrEmpty(i))
                .ToArray();

            var mediaAnalysis = await FFProbe.AnalyseAsync(videoFile);
            progressBar.Max = (int)mediaAnalysis.Duration.TotalSeconds;

            var command = Cli.Wrap("ffmpeg")
                .WithArguments(arguments, false);

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
                    progressBar.Refresh((int)timespan.TotalSeconds,
                        $"[FFMPEG]({speed:0.000}x) {Path.GetFileName(newVideoFile)}");
                }

                Logger?.LogTrace("[FFMpeg] {@Text}", text);
            }

            stopwatch.Stop();

            Logger.LogDebug("Merging {@OriginalVideoFile} with {@Subtitles} to {@NewVideoFile} took {@Elapsed}",
                videoFile, subtitlesFiles, newVideoFile, stopwatch.Elapsed);
        }
    }
}