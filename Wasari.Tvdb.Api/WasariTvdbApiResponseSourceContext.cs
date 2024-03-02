using System.Text.Json.Serialization;
using Wasari.Tvdb.Abstractions;
using Wasari.Tvdb.Api.Services;

namespace Wasari.Tvdb.Api;

[JsonSerializable(typeof(IEnumerable<WasariTvdbEpisode>))]
[JsonSerializable(typeof(TvdbApiErrorResponse))]
internal partial class WasariTvdbApiResponseSourceContext : JsonSerializerContext;