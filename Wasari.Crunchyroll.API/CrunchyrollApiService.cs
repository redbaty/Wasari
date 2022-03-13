using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Flurl;
using JsonExtensions.Http;
using JsonExtensions.Reading;
using Microsoft.Extensions.Caching.Memory;

namespace Wasari.Crunchyroll.API
{
    public class CrunchyrollApiService
    {
        internal CrunchyrollApiService(HttpClient httpClient, IMemoryCache memoryCache)
        {
            HttpClient = httpClient;
            MemoryCache = memoryCache;
        }

        private HttpClient HttpClient { get; }

        private ApiSignature ApiSignature { get; set; }
        
        private IMemoryCache MemoryCache { get; }

        private async Task<ApiSignature> GetApiSignature()
        {
            ApiSignature ??= await CreateApiSignature();
            return ApiSignature;
        }

        private async Task<ApiSignature> CreateApiSignature()
        {
            await using var responseStream = await HttpClient.GetStreamAsync("index/v2");
            using var jsonDocument = await JsonDocument.ParseAsync(responseStream);
            var root = jsonDocument.RootElement;

            return new ApiSignature
            {
                Bucket = root.GetPropertyByPath("cms.bucket").GetString(),
                Policy = root.GetPropertyByPath("cms.policy").GetString(),
                Signature = root.GetPropertyByPath("cms.signature").GetString(),
                KeyPairId = root.GetPropertyByPath("cms.key_pair_id").GetString(),
            };
        }

        private async Task<Url> BuildUrlFromSignature(string endpoint)
        {
            var signature = await GetApiSignature();
            return "cms/v2/"
                .AppendPathSegments(signature.Bucket, endpoint)
                .SetQueryParam("Policy", signature.Policy)
                .SetQueryParam("Signature", signature.Signature)
                .SetQueryParam("Key-Pair-Id", signature.KeyPairId)
                .SetQueryParam("locale", "en-US");
        }

        public IAsyncEnumerable<ApiEpisode> GetAllEpisodes(string seriesId)
        {
            return GetSeasons(seriesId)
                .Where(i => !i.IsDubbed && i.IsSubbed)
                .SelectMany(season => GetEpisodes(season.Id).Where(i => !i.IsDubbed && i.IsSubbed));
        }

        public async Task<ApiEpisode> GetEpisode(string episodeId)
        {
            var url = await BuildUrlFromSignature($"episodes/{episodeId}");
            var apiEpisode = await HttpClient.GetFromJsonAsync<ApiEpisode>(url);

            if (apiEpisode != null)
            {
                if (!string.IsNullOrEmpty(apiEpisode.StreamLink))
                {
                    var episodeStream = await GetStreams(apiEpisode.StreamLink);
                    apiEpisode.ApiEpisodeStreams = episodeStream;
                }
            }
            
            return apiEpisode;
        }

        public async IAsyncEnumerable<ApiEpisode> GetEpisodes(string seasonId)
        {
            var url = await BuildUrlFromSignature("episodes");
            url = url.SetQueryParam("season_id", seasonId);

            var responseJson = await HttpClient.GetJsonAsync(url);

            foreach (var jsonElement in responseJson.GetProperty("items").EnumerateArray())
            {
                var apiEpisode = jsonElement.Deserialize<ApiEpisode>();

                if (apiEpisode != null)
                {
                    if (!string.IsNullOrEmpty(apiEpisode.StreamLink))
                    {
                        var episodeStream = await GetStreams(apiEpisode.StreamLink);
                        apiEpisode.ApiEpisodeStreams = episodeStream;
                    }
                    
                    yield return apiEpisode;
                }
            }
        }

        public async Task<ApiSeason> GetSeason(string seasonId)
        {
            var url = await BuildUrlFromSignature($"seasons/{seasonId}");
            return await HttpClient.GetFromJsonAsync<ApiSeason>(url);
        }

        public async IAsyncEnumerable<ApiSeason> GetSeasons(string seriesId)
        {
            var url = await BuildUrlFromSignature("seasons");
            url = url.SetQueryParam("series_id", seriesId);

            var responseJson = await HttpClient.GetJsonAsync(url);
            foreach (var jsonElement in responseJson.GetProperty("items").EnumerateArray())
            {
                yield return jsonElement.Deserialize<ApiSeason>();
            }
        }

        public async Task<ApiSeries> GetSeriesInformation(string seriesId)
        {
            if (string.IsNullOrEmpty(seriesId))
                return null;

            var url = await BuildUrlFromSignature($"series/{seriesId}");
            return await HttpClient.GetFromJsonAsync<ApiSeries>(url);
        }

        public Task<ApiEpisodeStreams> GetStreams(string streamUrl)
        {
            return MemoryCache.GetOrCreateAsync(streamUrl, async _ =>
            {
                if (string.IsNullOrEmpty(streamUrl))
                    return null;

                var match = Regex.Match(streamUrl, @"videos\/(?<STREAM_ID>\w+)\/streams");
                var id = match.Groups["STREAM_ID"].Value;
                var url = await BuildUrlFromSignature($"videos/{id}/streams");

                return await HttpClient.GetFromJsonAsync<ApiEpisodeStreams>(url);
            });
        }
    }
}