using System.Net.Http.Json;
using Wasari.Tvdb.Models;

namespace Wasari.Tvdb;

internal class TvdbApi : ITvdbApi
{
    private readonly HttpClient _httpClient;

    public TvdbApi(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<TvdbResponse<IReadOnlyList<TvdbSearchResponseSeries>>?> SearchAsync(string query,
        string type = "series")
    {
        var url = $"/v4/search?query={query}&type={type}";
        return _httpClient.GetFromJsonAsync(url, TvdbSourceGenerationContext.Default.TvdbResponseIReadOnlyListTvdbSearchResponseSeries);
    }

    public Task<TvdbResponse<TvdbSeries>?> GetSeriesAsync(string id, string seasonType = "default", string lang = "eng",
        int page = 0)
    {
        var url = $"/v4/series/{id}/episodes/{seasonType}/{lang}?page={page}";
        return _httpClient.GetFromJsonAsync(url, TvdbSourceGenerationContext.Default.TvdbResponseTvdbSeries);
    }
}