using System.Text.Json.Serialization;

namespace Wasari.Crunchyroll.API
{
    public class ApiSeason
    {
        [JsonPropertyName("id")]
        public string Id { get; init; }

        [JsonPropertyName("title")]
        public string Title { get; init; }
        
        [JsonPropertyName("is_dubbed")]
        public bool IsDubbed { get; init; }
        
        [JsonPropertyName("is_subbed")]
        public bool IsSubbed { get; init; }
        
        [JsonPropertyName("season_number")]
        public int Number { get; init; }
        
        [JsonPropertyName("series_id")]
        public string SeriesId { get; init; }
    }
}