using System.Text.Json.Serialization;

namespace Wasari.Tvdb.Abstractions;

public record WasariTvdbEpisode(int Id, string Name, int? SeasonNumber, int? Number, bool IsMovie, string? Prefix, string SeriesId, int? CalculatedAbsoluteNumber)
{
    [JsonIgnore]
    public bool Matched { get; set; }
}