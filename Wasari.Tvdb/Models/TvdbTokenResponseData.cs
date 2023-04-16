using System.Text.Json.Serialization;

namespace Wasari.Tvdb.Models;

internal record TvdbTokenResponseData(
    [property: JsonPropertyName("token")] string Token
);