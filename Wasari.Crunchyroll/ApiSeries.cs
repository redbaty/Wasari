using System.Text.Json.Serialization;

namespace Wasari.Crunchyroll;

public class ApiSeries
{
    [JsonPropertyName("title")]
    public string Title { get; init; }
}