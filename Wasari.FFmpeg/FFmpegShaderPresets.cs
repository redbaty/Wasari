namespace Wasari.FFmpeg;

public class FFmpegShaderPresets
{
    public Dictionary<string, Func<IServiceProvider, IFFmpegShader>> ShadersFactory { get; } = new();
}