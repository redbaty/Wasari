namespace Wasari.FFmpeg;

public class FileShader : IFFmpegShader
{
    public FileShader(params FileInfo[] shaders)
    {
        Shaders = shaders.ToList();
    }

    public FileShader(IEnumerable<FileInfo> shaders)
    {
        Shaders = shaders.ToList();
    }

    public List<FileInfo> Shaders { get; }


    public Stream GetShaderStream()
    {
        var inMemoryStream = new MemoryStream();

        foreach (var shader in Shaders.Where(i => i.Exists))
        {
            using var fileStream = shader.OpenRead();
            fileStream.CopyTo(inMemoryStream);
        }

        inMemoryStream.Seek(0, SeekOrigin.Begin);
        return inMemoryStream;
    }
}