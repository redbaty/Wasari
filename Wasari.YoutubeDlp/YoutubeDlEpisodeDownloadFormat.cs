using System.Text.Json.Serialization;

namespace Wasari.YoutubeDlp;

public record YoutubeDlEpisodeDownloadFormat(
    [property: JsonPropertyName("url")]
    string? Url,
    [property: JsonPropertyName("vcodec")]
    string? Vcodec,
    [property: JsonPropertyName("acodec")]
    string? Acodec);