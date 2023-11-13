namespace Wasari.FFmpeg;

public record FFmpegOptions
{
    public bool UseHevc { get; set; }
    
    public string FileContainer { get; set; } = "mp4";

    public IFFmpegShader[]? Shaders { get; set; }

    public FFmpegResolution? Resolution { get; set; }

    public bool UseNvidiaAcceleration { get; set; }

    public bool UseTemporaryEncodingPath { get; set; }

    public int? Threads { get; set; }
}