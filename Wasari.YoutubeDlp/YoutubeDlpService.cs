using System.Text.Json;
using CliWrap;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wasari.App.Abstractions;

namespace Wasari.YoutubeDlp;

public class YoutubeDlpService
{
    public YoutubeDlpService(IOptions<YoutubeDlpOptions> options, ILogger<YoutubeDlpService> logger, IOptions<AuthenticationOptions> authenticationOptions)
    {
        Options = options;
        Logger = logger;
        AuthenticationOptions = authenticationOptions;
    }

    private IOptions<YoutubeDlpOptions> Options { get; }

    private IOptions<AuthenticationOptions> AuthenticationOptions { get; }

    private ILogger<YoutubeDlpService> Logger { get; }

    private IEnumerable<string> BuildArgumentsForEpisode(string[] urls)
    {
        if (Options.Value.IgnoreTls)
            yield return "--no-check-certificates";

        yield return "-J";

        if (!string.IsNullOrEmpty(Options.Value.Format))
            yield return $"-f \"{Options.Value.Format}\"";

        if (!string.IsNullOrEmpty(AuthenticationOptions.Value.Username))
            yield return $"-u \"{AuthenticationOptions.Value.Username}\"";

        if (!string.IsNullOrEmpty(AuthenticationOptions.Value.Password))
            yield return $"-p \"{AuthenticationOptions.Value.Password}\"";

        foreach (var url in urls)
        {
            yield return $"\"{url}\"";
        }
    }

    public IAsyncEnumerable<WasariEpisode> GetPlaylist(string url) => GetPlaylist(url, Array.Empty<string>());

    private IAsyncEnumerable<WasariEpisode> GetPlaylist(string url, params string[] additionalArguments)
    {
        return ExecuteYtdlp<YoutubeDlEpisode>(url, additionalArguments).Select(episode =>
        {
            var subtitleInputs = episode.Subtitles
                .SelectMany(i => i.Value
                    .Select(o => new WasariEpisodeInput(o.Url, i.Key, InputType.Subtitle)));

            var inputs = episode.RequestedDownloads?
                             .SelectMany(i => GetInputs(i))
                             .Concat(subtitleInputs)
                             .Cast<IWasariEpisodeInput>()
                             .ToArray()
                         ?? throw new InvalidOperationException("Failed to determine input URL");

            return new WasariEpisode(episode.EpisodeName ?? episode.Title, episode.SeriesName, episode.SeasonNumber, episode.Number, episode.AbsoluteNumber, _ => Task.FromResult<ICollection<IWasariEpisodeInput>>(inputs), TimeSpan.FromSeconds(episode.Duration));
        });
    }

    private static IEnumerable<WasariEpisodeInput> GetInputs(YoutubeDlEpisodeDownload episode)
    {
        if (!string.IsNullOrEmpty(episode.Url))
        {
            yield return new WasariEpisodeInput(episode.Url, episode.Language, string.IsNullOrEmpty(episode.Vcodec) ? InputType.Audio : InputType.Video);
        }
        else if (string.IsNullOrEmpty(episode.Url) && episode.Formats is { Count: > 0 })
        {
            foreach (var format in episode.Formats)
            {
                yield return new WasariEpisodeInput(format.Url ?? throw new InvalidOperationException("Failed to determine input URL"), episode.Language, string.IsNullOrEmpty(format.Vcodec) ? InputType.Audio : InputType.Video);
            }
        }
        else
        {
            throw new InvalidOperationException("Failed to determine input URL");
        }
    }

    private Command CreateCommand()
    {
        return Cli.Wrap("yt-dlp")
            .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Logger.LogInformation("YT-DLP output: {Message}", s)));
    }

    private IAsyncEnumerable<T> ExecuteYtdlp<T>(string url, params string[] additionalArguments)
    {
        return ExecuteYtdlp<T>(new[] { url }, additionalArguments);
    }

    private async IAsyncEnumerable<T> ExecuteYtdlp<T>(string[] urls, params string[] additionalArguments)
    {
        Logger.LogInformation("Getting information for {@Urls}", urls);

        var command = CreateCommand()
            .WithArguments(BuildArgumentsForEpisode(urls).Concat(additionalArguments), false);

        var jsonDocument = JsonDocument.Parse(await command.ExecuteAndGetStdOut());
        var type = jsonDocument.RootElement.GetProperty("_type").GetString();

        switch (type)
        {
            case "video":
                yield return jsonDocument.RootElement.Deserialize<T>() ?? throw new InvalidOperationException("Failed to deserialize yt-dlp");
                break;
            case "playlist":
                foreach (var jsonElement in jsonDocument.RootElement.GetProperty("entries").EnumerateArray())
                {
                    yield return jsonElement.Deserialize<T>() ?? throw new InvalidOperationException("Failed to deserialize yt-dlp");
                }

                break;
            default:
                throw new NotImplementedException($"No parser defined for type: {type}");
        }
    }
}