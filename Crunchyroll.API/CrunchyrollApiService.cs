using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Crunchyroll.API.Models;
using Flurl;
using JsonExtensions.Http;
using JsonExtensions.Reading;
using Microsoft.Extensions.DependencyInjection;

namespace Crunchyroll.API
{
    public class CrunchyrollApiService
    {
        public CrunchyrollApiService(IServiceProvider serviceProvider, HttpClient httpClient)
        {
            AuthenticationService = serviceProvider.GetService<CrunchyrollApiAuthenticationService>();
            HttpClient = httpClient;
        }

        private CrunchyrollApiAuthenticationService AuthenticationService { get; }

        private HttpClient HttpClient { get; }

        private ApiSignature ApiSignature { get; set; }

        private async Task EnsureAuthorizationHeader()
        {
            if (HttpClient.DefaultRequestHeaders.Contains("Authorization"))
            {
                return;
            }

            var accessToken = await AuthenticationService.GetAccessToken();
            HttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        }

        private async Task<ApiSignature> GetApiSignature()
        {
            ApiSignature ??= await CreateApiSignature();
            return ApiSignature;
        }

        private async Task<ApiSignature> CreateApiSignature()
        {
            await EnsureAuthorizationHeader();
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
            return $"cms/v2/"
                .AppendPathSegments(signature.Bucket, endpoint)
                .SetQueryParam("Policy", signature.Policy)
                .SetQueryParam("Signature", signature.Signature)
                .SetQueryParam("Key-Pair-Id", signature.KeyPairId)
                .SetQueryParam("locale", "en-US");
        }

        public async IAsyncEnumerable<ApiEpisode> GetAllEpisodes(string seriesId)
        {
            await foreach (var season in GetSeasons(seriesId))
            {
                await foreach (var episode in GetEpisodes(season.Id))
                {
                    yield return episode;
                }
            }
        }

        public async IAsyncEnumerable<ApiEpisode> GetEpisodes(string seasonId)
        {
            var url = await BuildUrlFromSignature("episodes");
            url = url.SetQueryParam("season_id", seasonId);
            
            var responseJson = await HttpClient.GetJsonAsync(url);

            foreach (var jsonElement in responseJson.GetProperty("items").EnumerateArray())
            {
                yield return jsonElement.Deserialize<ApiEpisode>();
            }
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
    }
}