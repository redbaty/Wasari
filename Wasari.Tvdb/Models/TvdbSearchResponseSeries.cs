using System.Text.Json.Serialization;

namespace Wasari.Tvdb.Models;

public record TvdbSearchResponseSeries(
    [property: JsonPropertyName("objectID")]
    string ObjectId,
    [property: JsonPropertyName("aliases")]
    IReadOnlyList<string>? Aliases,
    [property: JsonPropertyName("translations")]
    IReadOnlyDictionary<string, string>? Translations,
    [property: JsonPropertyName("country")]
    string Country,
    [property: JsonPropertyName("id")]
    string Id,
    [property: JsonPropertyName("image_url")]
    string ImageUrl,
    [property: JsonPropertyName("name")]
    string Name,
    [property: JsonPropertyName("first_air_time")]
    string FirstAirTime,
    [property: JsonPropertyName("overview")]
    string Overview,
    [property: JsonPropertyName("primary_language")]
    string PrimaryLanguage,
    [property: JsonPropertyName("primary_type")]
    string PrimaryType,
    [property: JsonPropertyName("status")]
    string Status,
    [property: JsonPropertyName("type")]
    string Type,
    [property: JsonPropertyName("tvdb_id")]
    string TvdbId,
    [property: JsonPropertyName("year")]
    string Year,
    [property: JsonPropertyName("slug")]
    string Slug,
    [property: JsonPropertyName("network")]
    string Network,
    [property: JsonPropertyName("thumbnail")]
    string Thumbnail
);