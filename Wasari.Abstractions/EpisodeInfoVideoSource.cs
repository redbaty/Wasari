namespace Wasari.Abstractions;

public class EpisodeInfoVideoSource
{
    public IEpisodeInfo? Episode { get; init; }

    public string? Url { get; init; }
        
    public string? LocalPath { get; set; }

    public string? Language { get; init; }
}