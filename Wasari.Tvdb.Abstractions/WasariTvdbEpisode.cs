using System.Text.Json.Serialization;

namespace Wasari.Tvdb.Abstractions;

public record WasariTvdbEpisode(string Name, int? SeasonNumber, int? Number, bool IsMovie, string? Prefix)
{
    [JsonIgnore]
    public bool Matched { get; set; }
}