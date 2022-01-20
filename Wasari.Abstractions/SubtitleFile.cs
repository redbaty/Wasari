namespace Wasari.Abstractions;

public class SubtitleFile : DownloadedFile
{
    public SubtitleFile()
    {
        Type = FileType.Subtitle;
    }

    public string? Language { get; init; }
}