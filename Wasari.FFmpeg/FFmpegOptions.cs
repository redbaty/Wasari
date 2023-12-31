namespace Wasari.FFmpeg;

public record FFmpegOptions
{
    public bool UseHevc { get; set; }

    public string FileContainer { get; set; } = "mkv";

    public IFFmpegShader[]? Shaders { get; set; }

    public FFmpegResolution? Resolution { get; set; }

    public bool UseNvidiaAcceleration { get; set; }

    public bool UseAmdAcceleration { get; set; }

    public bool UseTemporaryEncodingPath { get; set; }

    public int? Threads { get; set; }

    public HevcProfile HevcProfile { get; set; } = HevcProfile.Medium;

    public int? HevcQualityMin { get; set; }

    public int? HevcQualityMax { get; set; }

    public int? ShaderGpuIndex { get; set; }
}