using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.EventStream;
using CliWrap.Exceptions;
using FFMpegCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wasari.App.Abstractions;
using WasariEnvironment;

namespace Wasari.FFmpeg;

public partial class FFmpegService
{
    public FFmpegService(ILogger<FFmpegService> logger, IOptions<FFmpegOptions> options, EnvironmentService environmentService, IServiceProvider provider)
    {
        Logger = logger;
        Options = options;
        EnvironmentService = environmentService;
        Provider = provider;
    }

    private ILogger<FFmpegService> Logger { get; }

    private IOptions<FFmpegOptions> Options { get; }

    private EnvironmentService EnvironmentService { get; }

    private IServiceProvider Provider { get; }

    private static FileInfo CompileShaders(IEnumerable<IFFmpegShader> shaders)
    {
        var newFilePath = Path.GetTempFileName();
        using var fs = File.OpenWrite(newFilePath);
        using var streamWriter = new StreamWriter(fs);

        foreach (var shader in shaders)
        {
            using var shaderStream = shader.GetShaderStream();
            using var shaderStreamReader = new StreamReader(shaderStream);

            while (shaderStreamReader.ReadLine() is { } line) streamWriter.WriteLine(line);
        }

        streamWriter.Flush();
        streamWriter.Close();

        return new FileInfo(newFilePath);
    }

    private async IAsyncEnumerable<string> BuildArgumentsForEpisode(IWasariEpisode episode, string filePath)
    {
        if (Options.Value.Threads.HasValue)
            yield return $"-threads {Options.Value.Threads}";

        await using var providerScope = Provider.CreateAsyncScope();
        var inputs = await episode.InputsFactory(providerScope.ServiceProvider);

        if (inputs.Count == 0) throw new EmptyFFmpegInputsException(episode);

        var resolution = Options.Value.Resolution ?? inputs.Where(i => i.Type is InputType.Video or InputType.VideoWithAudio)
            .Select(i =>
            {
                var mediaAnalysis = FFProbe.Analyse(new Uri(i.Url));

                var videoStream = i is IWasariEpisodeInputStreamSelector { VideoIndex: not null } streamSelector ? mediaAnalysis.VideoStreams.Single(o => o.Index == streamSelector.VideoIndex.Value) : mediaAnalysis.PrimaryVideoStream;
                return videoStream == null ? null : new FFmpegResolution(videoStream.Width, videoStream.Height);
            }).Single(i => i != null);

        if (Options.Value.Shaders != null) yield return "-init_hw_device vulkan";

        var inputsOrdered = inputs.OrderBy(i => i.Type).ToArray();
        foreach (var input in inputsOrdered) yield return $"-i \"{input.Url}\"";

        for (var i = 0; i < inputsOrdered.Length; i++)
        {
            var input = inputsOrdered[i];

            if (input is IWasariEpisodeInputStreamSelector wasariEpisodeInput)
            {
                if (wasariEpisodeInput.AudioIndex.HasValue) yield return $"-map {i}:{wasariEpisodeInput.AudioIndex.Value}";

                if (wasariEpisodeInput.VideoIndex.HasValue) yield return $"-map {i}:{wasariEpisodeInput.VideoIndex.Value}";
            }
            else if (input.Type is InputType.Video or InputType.Subtitle)
            {
                yield return $"-map {i}";
                yield return $"-map -{i}:d";
            }
            else
            {
                yield return $"-map:a {i}";
            }
        }


        foreach (var inputsGroup in inputsOrdered.GroupBy(i => i.Type switch
                 {
                     InputType.Video => 0,
                     InputType.VideoWithAudio => 1,
                     InputType.Audio => 1,
                     InputType.Subtitle => 2,
                     _ => throw new ArgumentOutOfRangeException()
                 }))
        {
            var index = 0;

            foreach (var input in inputsGroup)
            {
                if (!string.IsNullOrEmpty(input.Language))
                {
                    var localLanguage = input.Language;

                    if (input.Language.Length == 4 && !input.Language.Contains('-')) localLanguage = $"{input.Language[..2]}-{input.Language[2..]}";

                    var cultureInfo = CultureInfo.GetCultureInfo(localLanguage);
                    yield return $"-metadata:s:{(input.Type == InputType.Subtitle ? "s" : "a")}:{index} language=\"{cultureInfo.ThreeLetterISOLanguageName}\"";
                }

                index++;
            }
        }

        if (Options.Value.Shaders is { Length: > 0 })
        {
            var shaderFile = CompileShaders(Options.Value.Shaders);
            var shaderPath = shaderFile.FullName;
            var sb = new StringBuilder("-filter_complex \"format=yuv420p,hwupload,libplacebo=");

            if (resolution != null) sb.Append($"w={resolution.Width}:h={resolution.Height}:");

            if (OperatingSystem.IsWindows()) shaderPath = shaderPath.Replace(@"\", @"\\\").Replace(":", @"\:");

            sb.Append($"custom_shader_path='{shaderPath}',hwdownload,format=yuv420p\"");

            yield return sb.ToString();
        }

        if (Options.Value.UseHevc)
        {
            if (Options.Value.UseNvidiaAcceleration && EnvironmentService.IsFeatureAvailable(EnvironmentFeatureType.NvidiaGpu))
                yield return "-c:v hevc_nvenc -rc vbr -cq 24 -qmin 24 -qmax 24 -profile:v main10 -pix_fmt p010le";
            else
                yield return "-crf 20 -pix_fmt yuv420p10le -c:v libx265 -tune animation -x265-params profile=main10";
        }
        else
        {
            yield return "-c:v copy";
        }

        yield return "-fflags +shortest -max_interleave_delta 0";
        yield return "-y";
        yield return $"\"{filePath}\"";
    }

    private static Command CreateCommand()
    {
        return new Command("ffmpeg")
            .WithWorkingDirectory(Environment.CurrentDirectory);
    }

    private string? GetTemporaryFile()
    {
        if (!Options.Value.UseTemporaryEncodingPath)
            return null;

        var tempFileName = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
        return Path.Combine(Path.GetTempPath(), $"{tempFileName}.mkv");
    }

    public async Task<bool> CheckIfVideoStreamIsValid(string filePath)
    {
        try
        {
            var fileAnalysis = await FFProbe.AnalyseAsync(filePath);

            if (fileAnalysis.ErrorData.Count > 0)
                return false;

            var videoDuration = fileAnalysis.VideoStreams.Max(o => o.Duration);

            if (videoDuration == TimeSpan.Zero && (fileAnalysis.PrimaryVideoStream?.Tags?.TryGetValue("DURATION", out var durationStr) ?? false))
            {
                var match = DurationRegex().Match(durationStr);
                var originalTrail = match.Groups["trail"].Value;
                var correctedTrail = originalTrail.TrimEnd('0');
                durationStr = durationStr.Replace(originalTrail, correctedTrail);

                if (TimeSpan.TryParse(durationStr, out var newVideoDuratio))
                {
                    videoDuration = newVideoDuratio;
                }
                else
                {
                    Logger.LogWarning("Failed to parse duration string: {DurationStr}", durationStr);
                    return false;
                }
            }

            var delta = fileAnalysis.Duration - videoDuration;
            return videoDuration >= fileAnalysis.Duration || delta < TimeSpan.FromSeconds(10);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Failed to check if video stream is valid");
            return false;
        }
    }

    public async Task DownloadEpisode<T>(T episode, string filePath, IProgress<FFmpegProgressUpdate>? progress) where T : IWasariEpisode
    {
        var tempFileName = GetTemporaryFile();
        var arguments = await BuildArgumentsForEpisode(episode, tempFileName ?? filePath).ToArrayAsync();
        var ffmpegCommand = CreateCommand()
            .WithValidation(CommandResultValidation.None)
            .WithArguments(arguments, false);

        await foreach (var commandEvent in ffmpegCommand.ListenAsync()) ProcessEvent(episode, progress, commandEvent, ffmpegCommand);

        if (tempFileName != null)
        {
            var destFileTempName = $"{filePath}.wasari_tmp";

            if (File.Exists(destFileTempName)) File.Delete(destFileTempName);

            File.Move(tempFileName, destFileTempName);
            File.Move(destFileTempName, filePath);
        }
    }

    private void ProcessEvent<T>(T episode, IProgress<FFmpegProgressUpdate>? progress, CommandEvent commandEvent, ICommandConfiguration ffmpegCommand) where T : IWasariEpisode
    {
        switch (commandEvent)
        {
            case ExitedCommandEvent exitedCommandEvent:
            {
                progress?.Report(new FFmpegProgressUpdate(1, 1));

                if (exitedCommandEvent.ExitCode != 0) throw new CommandExecutionException(ffmpegCommand, exitedCommandEvent.ExitCode, "Failed to run FFmpeg command");

                break;
            }
            case StartedCommandEvent startedCommandEvent:
                Logger.LogInformation("FFmpeg process started: {@ProcessId}", startedCommandEvent.ProcessId);
                break;
            default:
            {
                var text = commandEvent switch
                {
                    StandardErrorCommandEvent standardErrorCommandEvent => standardErrorCommandEvent.Text,
                    StandardOutputCommandEvent standardOutputCommandEvent => standardOutputCommandEvent.Text,
                    _ => null
                };

                if (progress != null
                    && episode.Duration.HasValue
                    && text != null
                    && text.GetValueFromRegex<double>(@"speed=(\d+.\d+)x", out var speed)
                    && text.GetValueFromRegex<string>(@"time=(\d+:\d+:\d+.\d+)", out var time)
                    && time != null
                   )
                {
                    var timespan = TimeSpan.Parse(time);
                    progress.Report(new FFmpegProgressUpdate(speed, timespan.TotalSeconds / episode.Duration.Value.TotalSeconds));
                }

                Logger.LogTrace("[FFMpeg] {@Text}", text);
                break;
            }
        }
    }

    [GeneratedRegex("\\d+\\:\\d+\\:\\d+(?<trail>\\.\\d+)")]
    private static partial Regex DurationRegex();
}