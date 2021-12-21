using System.Text.Json.Serialization;

namespace Wasari.Crunchyroll.API;

public class ApiSeries
{
    [JsonPropertyName("title")]
    public string Title { get; init; }
}