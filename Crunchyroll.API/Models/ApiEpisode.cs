using System.Text.Json.Serialization;
using Crunchyroll.API.Converters;

namespace Crunchyroll.API.Models
{
    public class ApiEpisode
    {
        [JsonPropertyName("id")]
        public string Id { get; init; }

        [JsonPropertyName("title")]
        public string Title { get; init; }
        
        [JsonPropertyName("episode")]
        [JsonConverter(typeof(NullIfEmptyConverter))]
        public string Episode { get; init; }
        
        [JsonPropertyName("episode_number")]
        public int? EpisodeNumber { get; init; }
        
        [JsonPropertyName("sequence_number")]
        public decimal SequenceNumber { get; init; }
        
        [JsonPropertyName("season_number")]
        public int SeasonNumber { get; init; }
        
        [JsonPropertyName("season_id")]
        public string SeasonId { get; init; }
        
        [JsonPropertyName("images")]
        [JsonConverter(typeof(ThumbnailsConverter))]
        public string[] ThumbnailIds { get; init; }

        [JsonPropertyName("is_clip")]
        public bool IsClip { get; init; }
    }
}