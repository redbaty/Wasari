using System.Text.Json.Serialization;

namespace Wasari.Tvdb.Models;

public record TvdbResponse<T>(
    [property: JsonPropertyName("status")]
    string Status,
    [property: JsonPropertyName("data")]
    T Data,
    [property: JsonPropertyName("links")]
    TvdbLinks Links
);