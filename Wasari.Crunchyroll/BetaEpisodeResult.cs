using Wasari.Abstractions;

namespace Wasari.Crunchyroll;

internal class BetaEpisodeResult
{
    public string Url { get; set; }
        
    public DownloadedFile[] Files { get; set; }
}