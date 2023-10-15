namespace Wasari.YoutubeDlp;

public record YoutubeDlpOptions
{
    public string? Format { get; set; }

    public bool IgnoreTls { get; set; }
}