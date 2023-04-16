using System.Text.Json.Serialization;

namespace Wasari.Tvdb.Models;

public record TvdbStatus(
    [property: JsonPropertyName("id")] int? Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("recordType")] string RecordType,
    [property: JsonPropertyName("keepUpdated")] bool? KeepUpdated
);