using System.Text.Json.Serialization;

namespace Wasari.Tvdb.Models;

public record TvdbLinks(
    [property: JsonPropertyName("prev")]
    string? Prev,
    [property: JsonPropertyName("self")]
    string Self,
    [property: JsonPropertyName("next")]
    string? Next,
    [property: JsonPropertyName("total_items")]
    int? TotalItems,
    [property: JsonPropertyName("page_size")]
    int? PageSize
);