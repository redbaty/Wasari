using Microsoft.Extensions.Options;
using Wasari.YoutubeDlp;

namespace Wasari.App;

public class CrunchyrollBetaDownloadModifier : IDownloadModifier
{
    public CrunchyrollBetaDownloadModifier(IOptions<DownloadOptions> options)
    {
        Options = options;
    }

    private IOptions<DownloadOptions> Options { get; }
    
    public IAsyncEnumerable<YoutubeDlEpisode> Modify(IAsyncEnumerable<YoutubeDlEpisode> episodes) => episodes
        .FillSeasonAbsoluteNumbers()
        .Where(i => Options.Value.IncludeDubs || i.Language == "ja-JP")
        .Group()
        .FillEpisodesAbsoluteNumbers()
        .FixEpisodesNumbers();
}