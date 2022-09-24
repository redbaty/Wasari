using System.Text.Json;
using System.Threading.Channels;
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
    
    public IAsyncEnumerable<WasariEpisode> GetPlaylist(string url, params string[] additionalArguments)
    {
        return ExecuteYtdlp<YoutubeDlEpisode>(url, additionalArguments).Select(episode =>
        {
            var subtitleInputs = episode.Subtitles
                .SelectMany(i => i.Value
                    .Select(o => new WasariEpisodeInput(o.Url, i.Key, InputType.Subtitle)));

            var inputs = episode.RequestedDownloads
                .Select(i => new WasariEpisodeInput(i.Url, i.Language, string.IsNullOrEmpty(i.Vcodec) ? InputType.Audio : InputType.Video))
                .Concat(subtitleInputs)
                .Cast<IWasariEpisodeInput>()
                .ToArray();

            return new WasariEpisode(episode.Title, episode.SeriesName, episode.SeasonNumber, episode.Number, episode.AbsoluteNumber, _ => Task.FromResult<ICollection<IWasariEpisodeInput>>(inputs), TimeSpan.FromSeconds(episode.Duration));
        });
    }

    public IAsyncEnumerable<YoutubeDlFlatPlaylistEpisode> GetFlatPlaylist(string url)
    {
        return ExecuteYtdlp<YoutubeDlFlatPlaylistEpisode>(url, "--flat-playlist");
    }

    private Command CreateCommand()
    {
        return Cli.Wrap("yt-dlp")
            .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Logger.LogInformation("YT-DLP output: {Message}", s)));
    }

    public async IAsyncEnumerable<YoutubeDlEpisode> GetEpisodes(IAsyncEnumerable<YoutubeDlFlatPlaylistEpisode> episodes)
    {
        var queue = Channel.CreateUnbounded<YoutubeDlEpisode>();

        var parallelTask = Parallel.ForEachAsync(episodes, async (episode, token) =>
            {
                await foreach (var youtubeDlEpisode in GetEpisodes(episode.Type == "video" && !string.IsNullOrEmpty(episode.WebpageUrl) ? episode.WebpageUrl : episode.Url).WithCancellation(token))
                {
                    await queue.Writer.WriteAsync(youtubeDlEpisode, token);
                }
            })
            .ContinueWith(_ => { queue.Writer.Complete(); });

        await foreach (var youtubeDlEpisode in queue.Reader.ReadAllAsync())
        {
            yield return youtubeDlEpisode;
        }

        await parallelTask;
    }

    private IAsyncEnumerable<YoutubeDlEpisode> GetEpisodes(string url)
    {
        return ExecuteYtdlp<YoutubeDlEpisode>(url);
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