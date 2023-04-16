using Refit;
using Wasari.Tvdb.Abstractions;

namespace Wasari.Tvdb.Api.Client;

public interface IWasariTvdbApi
{
    [Get("/episodes")]
    public Task<IReadOnlyList<Episode>> GetEpisodesAsync(string query);
}