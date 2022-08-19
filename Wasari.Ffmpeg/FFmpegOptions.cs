namespace Wasari.FFmpeg;

public record FFmpegOptions
{
    public bool UseHevc { get; set; }
    
    public bool UseNvidiaAcceleration { get; set; }
    
    public bool UseTemporaryEncodingPath { get; set; }
}