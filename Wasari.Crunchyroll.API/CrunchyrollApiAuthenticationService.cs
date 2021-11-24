using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Wasari.Crunchyroll.API
{
    internal class CrunchyrollApiAuthenticationService
    {
        public CrunchyrollApiAuthenticationService(HttpClient httpClient)
        {
            HttpClient = httpClient;
        }

        private HttpClient HttpClient { get; }

        private string AccessToken { get; set; }

        public async Task<string> GetAccessToken()
        {
            AccessToken ??= await CreateAccessToken();
            return AccessToken;
        }

        private async Task<string> CreateAccessToken()
        {
            using var formUrlEncodedContent = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("grant_type", "client_id") });
            using var authResponse = await HttpClient.PostAsync("auth/v1/token", formUrlEncodedContent);
            await using var responseStream = await authResponse.Content.ReadAsStreamAsync();
            var jsonDocument = await JsonDocument.ParseAsync(responseStream);
            return jsonDocument.RootElement.GetProperty("access_token").GetString();
        }
    }
}