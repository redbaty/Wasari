using Wasari.Abstractions;

namespace Wasari.YoutubeDl;

public class YoutubeDlEpisodeResult
{
    public IEpisodeInfo Episode { get; init; }
        
    public ICollection<YoutubeDlResult> Results { get; init; }
}