using System.Text.Json.Serialization;

namespace Wasari.Tvdb.Models;

public record TvdbEpisode(
    [property: JsonPropertyName("id")] int? Id,
    [property: JsonPropertyName("seriesId")] int? SeriesId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("aired")] string Aired,
    [property: JsonPropertyName("runtime")] int? Runtime,
    [property: JsonPropertyName("nameTranslations")] object NameTranslations,
    [property: JsonPropertyName("overview")] string Overview,
    [property: JsonPropertyName("overviewTranslations")] object OverviewTranslations,
    [property: JsonPropertyName("image")] string Image,
    [property: JsonPropertyName("imageType")] int? ImageType,
    [property: JsonPropertyName("isMovie")] int? IsMovie,
    [property: JsonPropertyName("seasons")] object Seasons,
    [property: JsonPropertyName("number")] int? Number,
    [property: JsonPropertyName("seasonNumber")] int? SeasonNumber,
    [property: JsonPropertyName("lastUpdated")] string LastUpdated,
    [property: JsonPropertyName("finaleType")] string FinaleType,
    [property: JsonPropertyName("airsBeforeSeason")] int? AirsBeforeSeason,
    [property: JsonPropertyName("airsBeforeEpisode")] int? AirsBeforeEpisode,
    [property: JsonPropertyName("year")] string Year,
    [property: JsonPropertyName("airsAfterSeason")] int? AirsAfterSeason
);