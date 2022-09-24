using System.Globalization;
using CliWrap;
using CliWrap.EventStream;
using CliWrap.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wasari.App.Abstractions;
using WasariEnvironment;

namespace Wasari.FFmpeg;

public class FFmpegService
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

    private async IAsyncEnumerable<string> BuildArgumentsForEpisode(IWasariEpisode episode, string filePath)
    {
        await using var providerScope = Provider.CreateAsyncScope();
        var inputs = await episode.InputsFactory(providerScope.ServiceProvider);
        var inputsOrdered = inputs.OrderBy(i => i.Type).ToArray();
        foreach (var input in inputsOrdered)
        {
            yield return $"-i \"{input.Url}\"";
        }

        for (var i = 0; i < inputsOrdered.Length; i++)
        {
            var input = inputsOrdered[i];

            if (input is IWasariEpisodeInputStreamSelector wasariEpisodeInput)
            {
                if (wasariEpisodeInput.AudioIndex.HasValue)
                {
                    yield return $"-map {i}:{wasariEpisodeInput.AudioIndex.Value}";
                }

                if (wasariEpisodeInput.VideoIndex.HasValue)
                {
                    yield return $"-map {i}:{wasariEpisodeInput.VideoIndex.Value}";
                }
            }
            else if (input.Type is InputType.Video or InputType.Subtitle)
            {
                yield return $"-map {i}";
                yield return $"-map -{i}:d";
            }
            else
                yield return $"-map:a {i}";
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
                    var cultureInfo = CultureInfo.GetCultureInfo(input.Language);
                    yield return $"-metadata:s:{(input.Type == InputType.Subtitle ? "s" : "a")}:{index} language=\"{cultureInfo.ThreeLetterISOLanguageName}\"";
                }
        
                index++;
            }
        }

        if (Options.Value.UseHevc)
        {
            if (Options.Value.UseNvidiaAcceleration && EnvironmentService.IsFeatureAvailable(EnvironmentFeatureType.NvidiaGpu))
            {
                yield return "-c:v hevc_nvenc -rc vbr -cq 24 -qmin 24 -qmax 24 -profile:v main10 -pix_fmt p010le";
            }
            else
            {
                yield return "-crf 20 -pix_fmt yuv420p10le -c:v libx265 -tune animation -x265-params profile=main10";
            }
        }
        else
        {
            yield return "-crf 20";
        }

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

    public async Task DownloadEpisode<T>(T episode, string filePath, IProgress<double>? progress) where T : IWasariEpisode
    {
        var tempFileName = GetTemporaryFile();
        var arguments = await BuildArgumentsForEpisode(episode, tempFileName ?? filePath).ToArrayAsync();
        var ffmpegCommand = CreateCommand()
            .WithValidation(CommandResultValidation.None)
            .WithArguments(arguments, false);

        await foreach (var commandEvent in ffmpegCommand.ListenAsync())
        {
            switch (commandEvent)
            {
                case ExitedCommandEvent exitedCommandEvent:
                {
                    progress?.Report(1);
                
                    if (exitedCommandEvent.ExitCode != 0)
                    {
                        throw new CommandExecutionException(ffmpegCommand, exitedCommandEvent.ExitCode, "Failed to run FFmpeg command");
                    }

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

                    if (text != null
                        && text.GetValueFromRegex<double>(@"speed=(\d+.\d+)x", out var speed)
                        && text.GetValueFromRegex<string>(@"time=(\d+:\d+:\d+.\d+)", out var time)
                        && time != null
                        && progress != null
                        && episode.Duration.HasValue)
                    {
                        var timespan = TimeSpan.Parse(time);
                        var delta = timespan.TotalSeconds / episode.Duration.Value.TotalSeconds;
                        progress.Report(delta);
                    }

                    Logger.LogTrace("[FFMpeg] {@Text}", text);
                    break;
                }
            }
        }

        if (tempFileName != null)
        {
            File.Move(tempFileName, filePath);
        }

        
    }
}