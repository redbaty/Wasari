using System.Text.Json.Serialization;

namespace Wasari.Tvdb.Models;

public record TvdbLoginRequest([property: JsonPropertyName("apikey")] string ApiKey, [property: JsonPropertyName("pin")] string Pin);