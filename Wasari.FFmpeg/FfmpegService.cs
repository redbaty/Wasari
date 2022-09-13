using System.Globalization;
using CliWrap;
using CliWrap.EventStream;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wasari.App.Abstractions;
using WasariEnvironment;

namespace Wasari.FFmpeg;

public class FFmpegService
{
    public FFmpegService(ILogger<FFmpegService> logger, IOptions<FFmpegOptions> options, EnvironmentService environmentService)
    {
        Logger = logger;
        Options = options;
        EnvironmentService = environmentService;
    }

    private ILogger<FFmpegService> Logger { get; }

    private IOptions<FFmpegOptions> Options { get; }
    
    private EnvironmentService EnvironmentService { get; }
    
    private IEnumerable<string> BuildArgumentsForEpisode(IWasariEpisode episode, string filePath)
    {
        var inputsOrdered = episode.Inputs.OrderBy(i => i.Type).ToArray();
        foreach (var input in inputsOrdered)
        {
            yield return $"-i \"{input.Url}\"";
        }
        
        for (var i = 0; i < inputsOrdered.Length; i++)
        {
            var input = inputsOrdered[i];

            if (input.Type is InputType.Video or InputType.Subtitle)
                yield return $"-map {i}";
            else
                yield return $"-map:a {i}";

            yield return $"-map -{i}:d";
        }
        
        foreach (var inputsGroup in inputsOrdered.GroupBy(i => i.Type))
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

    private Command CreateCommand()
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
        var arguments = BuildArgumentsForEpisode(episode, tempFileName ?? filePath);
        var command = CreateCommand()
            .WithArguments(arguments, false);

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
                && time != null
                && progress != null
                && episode.Duration.HasValue)
            {
                var timespan = TimeSpan.Parse(time);
                var delta = timespan.TotalSeconds / episode.Duration.Value.TotalSeconds;
                progress.Report(delta);
            }

            Logger.LogTrace("[FFMpeg] {@Text}", text);
        }

        if (tempFileName != null)
        {
            File.Move(tempFileName, filePath);
        }
    }
}