using System.Text.Json.Serialization;

namespace Wasari.Tvdb.Models;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(TvdbResponse<IReadOnlyList<TvdbSearchResponseSeries>>))]
[JsonSerializable(typeof(TvdbResponse<TvdbSeries>))]
[JsonSerializable(typeof(TvdbResponse<TvdbTokenResponseData?>))]
[JsonSerializable(typeof(TvdbLoginRequest))]
internal partial class TvdbSourceGenerationContext : JsonSerializerContext
{
}