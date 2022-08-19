namespace Wasari.YoutubeDlp;

public record YoutubeDlpOptions
{
    public string? Username { get; set; }
    
    public string? Password { get; set; }

    public string? Format { get; set; }

    public string? CookieFilePath { get; set; } = Path.Combine(Path.GetTempPath(), $"{Path.GetTempFileName()}.txt");
}