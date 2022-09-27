using System.Text.Json.Serialization;

namespace Wasari.YoutubeDlp;

public record YoutubeDlEpisodeDownload(
    [property: JsonPropertyName("url")]
    string? Url,
    [property: JsonPropertyName("vcodec")]
    string? Vcodec,
    [property: JsonPropertyName("acodec")]
    string? Acodec,
    [property: JsonPropertyName("language")]
    string Language,
    [property: JsonPropertyName("requested_formats")]
    IReadOnlyList<YoutubeDlEpisodeDownloadFormat> Formats);