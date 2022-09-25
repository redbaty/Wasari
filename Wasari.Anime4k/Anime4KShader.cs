using System.Reflection;
using Wasari.FFmpeg;

namespace Wasari.Anime4k;

public class Anime4KShader : IFFmpegShader
{
    public Stream GetShaderStream()
    {
        return Assembly.GetAssembly(typeof(Anime4KShader))?.GetManifestResourceStream("Wasari.Anime4k.main.glsl") ?? throw new InvalidOperationException("Failed to get anime 4k file");
    }
}