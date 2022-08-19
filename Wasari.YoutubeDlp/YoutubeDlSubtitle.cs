using System.Text.Json.Serialization;

namespace Wasari.YoutubeDlp;

public record YoutubeDlSubtitle(
    [property: JsonPropertyName("url")]
    string Url,
    [property: JsonPropertyName("ext")]
    string Ext);