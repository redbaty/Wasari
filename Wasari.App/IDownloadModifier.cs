using Wasari.YoutubeDlp;

namespace Wasari.App;

public interface IDownloadModifier
{
    IAsyncEnumerable<YoutubeDlEpisode> Modify(IAsyncEnumerable<YoutubeDlEpisode> episodes);
}