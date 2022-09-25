namespace Wasari.FFmpeg;

public record FFmpegResolution(int Width, int Height)
{
    public static FFmpegResolution FourK { get; } = new(3840, 2160);
}