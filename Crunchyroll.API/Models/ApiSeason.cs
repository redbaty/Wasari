using System.Text.Json.Serialization;

namespace Crunchyroll.API.Models
{
    public class ApiSeason
    {
        [JsonPropertyName("id")]
        public string Id { get; init; }

        [JsonPropertyName("title")]
        public string Title { get; init; }
    }
}