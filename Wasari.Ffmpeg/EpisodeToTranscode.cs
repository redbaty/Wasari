namespace Wasari.Ffmpeg;

public class EpisodeToTranscode
{
    public EpisodeVideoSource[] VideoSources { get; init; }

    public EpisodeSubtitleSource[] Subtitles { get; init; }
}