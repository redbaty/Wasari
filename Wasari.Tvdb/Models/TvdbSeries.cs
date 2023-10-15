using System.Text.Json.Serialization;

namespace Wasari.Tvdb.Models;

public record TvdbSeries(
    [property: JsonPropertyName("id")]
    int? Id,
    [property: JsonPropertyName("name")]
    string Name,
    [property: JsonPropertyName("slug")]
    string Slug,
    [property: JsonPropertyName("image")]
    string Image,
    [property: JsonPropertyName("nameTranslations")]
    IReadOnlyList<string> NameTranslations,
    [property: JsonPropertyName("overviewTranslations")]
    IReadOnlyList<string> OverviewTranslations,
    [property: JsonPropertyName("aliases")]
    IReadOnlyList<TvdbAlias> Aliases,
    [property: JsonPropertyName("firstAired")]
    string FirstAired,
    [property: JsonPropertyName("lastAired")]
    string LastAired,
    [property: JsonPropertyName("nextAired")]
    string NextAired,
    [property: JsonPropertyName("score")]
    int? Score,
    [property: JsonPropertyName("status")]
    TvdbStatus Status,
    [property: JsonPropertyName("originalCountry")]
    string OriginalCountry,
    [property: JsonPropertyName("originalLanguage")]
    string OriginalLanguage,
    [property: JsonPropertyName("defaultSeasonType")]
    int? DefaultSeasonType,
    [property: JsonPropertyName("isOrderRandomized")]
    bool? IsOrderRandomized,
    [property: JsonPropertyName("lastUpdated")]
    string LastUpdated,
    [property: JsonPropertyName("averageRuntime")]
    int? AverageRuntime,
    [property: JsonPropertyName("episodes")]
    IReadOnlyList<TvdbEpisode> Episodes,
    [property: JsonPropertyName("overview")]
    string Overview,
    [property: JsonPropertyName("year")]
    string Year
);