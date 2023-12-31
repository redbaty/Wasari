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

    private HevcOptions GetHevcOptions()
    {
        return Options.Value.HevcProfile switch
        {
            HevcProfile.High => new HevcOptions(18, 18),
            HevcProfile.Medium => new HevcOptions(24, 24),
            HevcProfile.Low => new HevcOptions(30, 30),
            HevcProfile.Custom => new HevcOptions(Options.Value.HevcQualityMin ?? 24, Options.Value.HevcQualityMax ?? 24),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

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

        if (Options.Value.Shaders != null)
        {
            var gpuSelector = Options.Value.ShaderGpuIndex.HasValue ? $":{Options.Value.ShaderGpuIndex}" : string.Empty;

            yield return $"-init_hw_device vulkan{gpuSelector}";
        }

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
            else if (input.Type == InputType.Video)
            {
                yield return $"-map {i}";
                yield return $"-map -{i}:d";
            }
            else if (input.Type == InputType.Subtitle)
            {
                yield return $"-map {i}:s";
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
            if (Options.Value is { UseNvidiaAcceleration: true, UseAmdAcceleration: true } && EnvironmentService.IsFeatureAvailable(EnvironmentFeatureType.NvidiaGpu, EnvironmentFeatureType.AmdGpu))
                throw new MultipleEncodersException("Cannot use both Nvidia and AMD acceleration at the same time");

            var hevcOptions = GetHevcOptions();

            if (Options.Value.UseNvidiaAcceleration && EnvironmentService.IsFeatureAvailable(EnvironmentFeatureType.NvidiaGpu))
                yield return $"-c:v hevc_nvenc -rc vbr -qmin {hevcOptions.Qmin} -qmax {hevcOptions.Qmax} -profile:v main10 -pix_fmt p010le";
            else if (Options.Value.UseAmdAcceleration && EnvironmentService.IsFeatureAvailable(EnvironmentFeatureType.AmdGpu))
                yield return $"-c:v hevc_amf -rc cbr -qmin {hevcOptions.Qmin} -qmax {hevcOptions.Qmax} -pix_fmt p010le";
            else
                yield return "-crf 20 -pix_fmt yuv420p10le -c:v libx265 -tune animation -x265-params profile=main10";
        }
        else
        {
            yield return "-c:v copy";
        }

        var fileExtension = Path.GetExtension(filePath);
        var isMp4 = fileExtension == ".mp4";

        if (isMp4)
            yield return "-c:s mov_text";

        yield return "-y";
        yield return $"\"{filePath}\"";
    }

    private static Command CreateCommand()
    {
        return new Command("ffmpeg")
            .WithWorkingDirectory(Environment.CurrentDirectory);
    }

    private string? GetTemporaryFile(string extension, string? baseFilePath = null)
    {
        if (baseFilePath != null)
        {
            var fileName = Path.GetFileNameWithoutExtension(baseFilePath);
            var fileDirectory = Path.GetDirectoryName(baseFilePath);

            return $"{fileDirectory}{Path.DirectorySeparatorChar}{fileName}_wasari_tmp{extension}";
        }

        if (!Options.Value.UseTemporaryEncodingPath)
            return null;

        var tempFileName = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
        return Path.Combine(Path.GetTempPath(), $"{tempFileName}.{extension}");
    }

    public TimeSpan? GetVideoDuration(IMediaAnalysis mediaAnalysis)
    {
        var videoDuration = mediaAnalysis.VideoStreams.Max(o => o.Duration);

        if (videoDuration == TimeSpan.Zero && (mediaAnalysis.PrimaryVideoStream?.Tags?.TryGetValue("DURATION", out var durationStr) ?? false))
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
                return null;
            }
        }

        return videoDuration == TimeSpan.Zero ? null : videoDuration;
    }

    public async Task<bool> CheckIfVideoStreamIsValid(string filePath)
    {
        try
        {
            var fileAnalysis = await FFProbe.AnalyseAsync(filePath);

            if (fileAnalysis.ErrorData.Count > 0)
                return false;

            var videoDuration = GetVideoDuration(fileAnalysis);

            if (videoDuration == null)
                return false;

            var delta = fileAnalysis.Duration - videoDuration;
            var isValid = videoDuration >= fileAnalysis.Duration || delta < TimeSpan.FromSeconds(10);

            if (!isValid) Logger.LogWarning("File was found to be invalid: {FilePath}, the difference between the video duration and the file duration is {Delta}", filePath, delta);

            return isValid;
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Failed to check if video stream is valid");
            return false;
        }
    }

    public async Task DownloadEpisode<T>(T episode, string filePath, IProgress<FFmpegProgressUpdate>? progress) where T : IWasariEpisode
    {
        var tempFileName = GetTemporaryFile(Path.GetExtension(filePath));
        var outputFile = tempFileName ?? filePath;
        var arguments = await BuildArgumentsForEpisode(episode, outputFile).ToArrayAsync();
        var ffmpegCommand = CreateCommand()
            .WithValidation(CommandResultValidation.None)
            .WithArguments(arguments, false);

        await foreach (var commandEvent in ffmpegCommand.ListenAsync()) ProcessEvent(episode, progress, commandEvent, ffmpegCommand);

        var checkIfVideoStreamIsValid = await CheckIfVideoStreamIsValid(outputFile);

        if (!checkIfVideoStreamIsValid)
        {
            var outputDirectory = Path.Combine(Path.GetTempPath(), $"{Path.GetFileName(filePath)}-subs");
            if (!Directory.Exists(outputDirectory)) Directory.CreateDirectory(outputDirectory);

            var mediaAnalysis = await FFProbe.AnalyseAsync(outputFile);
            var duration = GetVideoDuration(mediaAnalysis);

            if (duration != null && mediaAnalysis.SubtitleStreams.All(x => x.CodecName == "ass"))
            {
                var subtitleFiles = await ExtractSubtitles(outputFile, outputDirectory, mediaAnalysis, duration.Value).ToListAsync();
                var modifiedSubtitlesFile = await subtitleFiles
                    .ToAsyncEnumerable()
                    .SelectAwait(i => FixSubtitleDuration(i, duration.Value))
                    .ToHashSetAsync();

                var outputFileName = $"{outputFile}.modified{Path.GetExtension(outputFile)}";
                await ReplaceSubtitles(outputFile, modifiedSubtitlesFile, outputFileName);
                Directory.Delete(outputDirectory, true);

                if (await CheckIfVideoStreamIsValid(outputFileName))
                {
                    File.Delete(outputFile);
                    File.Move(outputFileName, outputFile);
                    Logger.LogInformation("Video duration has been fixed");
                }
            }
        }

        if (tempFileName != null)
        {
            var destFileTempName = $"{filePath}.wasari_tmp";

            if (File.Exists(destFileTempName)) File.Delete(destFileTempName);

            File.Move(tempFileName, destFileTempName);
            File.Move(destFileTempName, filePath);
        }
    }

    private static async ValueTask<SubtitleFile> FixSubtitleDuration(SubtitleFile subtitleFile, TimeSpan duration)
    {
        var filePath = subtitleFile.FilePath;
        var modifiedFilePath = $"{filePath}.modified{Path.GetExtension(filePath)}";

        await using var readStream = File.OpenRead(filePath);
        using var readStreamReader = new StreamReader(readStream);

        await using var writeStream = File.OpenWrite(modifiedFilePath);
        await using var streamWriter = new StreamWriter(writeStream);
        var hasReachedEvents = false;

        while (await readStreamReader.ReadLineAsync() is { } line)
        {
            if (!hasReachedEvents && line.StartsWith("[Events]", StringComparison.InvariantCultureIgnoreCase))
            {
                hasReachedEvents = true;
            }
            else if (hasReachedEvents)
            {
                var match = AssEventRegex().Match(line);

                if (match.Success)
                {
                    var dateEnd = TimeSpan.Parse(match.Groups["dateEnd"].Value);

                    if (dateEnd > duration)
                    {
                        line = line.Replace(match.Groups["dateEnd"].Value, duration.ToString(@"hh\:mm\:ss\.ff"));
                        await streamWriter.WriteLineAsync(line);
                        break;
                    }
                }
            }

            await streamWriter.WriteLineAsync(line);
        }

        await streamWriter.FlushAsync();
        return new SubtitleFile(modifiedFilePath, subtitleFile.Language);
    }

    private async IAsyncEnumerable<SubtitleFile> ExtractSubtitles(string filePath, string outputDirectory, IMediaAnalysis mediaAnalysis, TimeSpan duration)
    {
        if (mediaAnalysis.PrimaryVideoStream != null)
        {
            foreach (var mediaAnalysisSubtitleStream in mediaAnalysis.SubtitleStreams)
            {
                var outputFile = await ExtractSubtitle(filePath, outputDirectory, duration, mediaAnalysisSubtitleStream);
                yield return outputFile;
            }

            Logger.LogInformation("Extracted subtitles from {FilePath} to {OutputDirectory}", filePath, outputDirectory);
        }
    }

    private CommandTask<CommandResult> ReplaceSubtitles(string videoFilePath, ICollection<SubtitleFile> subtitlesFilePaths, string outputFileName)
    {
        var arguments = CreateArguments().ToArray();
        var ffmpegCommand = CreateCommand()
            .WithArguments(arguments, false);

        Logger.LogInformation("Replacing subtitles for {VideoFilePath} with {SubtitlesFilePaths} to {OutputFileName}", videoFilePath, subtitlesFilePaths, outputFileName);

        return ffmpegCommand.ExecuteAsync();

        IEnumerable<string> CreateArguments()
        {
            yield return $"-i \"{videoFilePath}\"";

            foreach (var subtitlesFilePath in subtitlesFilePaths) yield return $"-i \"{subtitlesFilePath.FilePath}\"";

            yield return "-map 0";
            yield return "-map -0:s";

            foreach (var subtitle in subtitlesFilePaths.Select((s, y) => new { Index = y, s.Language }))
            {
                yield return $"-map {subtitle.Index + 1}";

                if (subtitle.Language != null)
                    yield return $"-metadata:s:s:{subtitle.Index} language=\"{subtitle.Language}\"";
            }

            yield return "-c copy";
            yield return "-y";
            yield return $"\"{outputFileName}\"";
        }
    }

    private static async ValueTask<SubtitleFile> ExtractSubtitle(string filePath, string outputDirectory, TimeSpan duration, MediaStream mediaAnalysisSubtitleStream)
    {
        var outputFileName = $"{outputDirectory}{Path.DirectorySeparatorChar}sub{mediaAnalysisSubtitleStream.Index:00}.{mediaAnalysisSubtitleStream.CodecName}";

        var arguments = new[]
        {
            $"-i \"{filePath}\"",
            $"-map 0:{mediaAnalysisSubtitleStream.Index}",
            $"-t \"{duration}\"",
            "-y",
            $"\"{outputFileName}\""
        };

        var ffmpegCommand = CreateCommand()
            .WithArguments(arguments, false);

        await ffmpegCommand.ExecuteAsync();
        return new SubtitleFile(outputFileName, mediaAnalysisSubtitleStream.Language);
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

    [GeneratedRegex(@"^Dialogue:\s\d+,(?<dateStart>\d+:\d+:\d+\.\d+),(?<dateEnd>\d+:\d+:\d+\.\d+),")]
    private static partial Regex AssEventRegex();
}