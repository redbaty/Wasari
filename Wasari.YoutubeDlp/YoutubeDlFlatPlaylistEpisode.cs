using System.Text.Json.Serialization;

namespace Wasari.YoutubeDlp;

public record YoutubeDlFlatPlaylistEpisode(
    [property: JsonPropertyName("url")]
    string Url,
    [property: JsonPropertyName("season_number")]
    int SeasonNumber,
    [property: JsonPropertyName("episode_number")]
    int Number,
    [property: JsonPropertyName("_type")]
    string Type,
    [property: JsonPropertyName("webpage_url")]
    string? WebpageUrl
);