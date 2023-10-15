using System.Text.Json.Serialization;

namespace Wasari.Tvdb.Models;

public record TvdbAlias(
    [property: JsonPropertyName("language")]
    string Language,
    [property: JsonPropertyName("name")]
    string Name
);