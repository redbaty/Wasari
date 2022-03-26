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

        private string AnonymousAccessToken { get; set; }
        
        private string AuthenticatedAccessToken { get; set; }

        public async Task<string> GetAccessToken()
        {
            AnonymousAccessToken ??= await CreateAccessToken();
            return AnonymousAccessToken;
        }
        
        public async Task<string> GetAccessToken(string username, string password)
        {
            AuthenticatedAccessToken ??= await CreateAccessToken(username, password);
            return AuthenticatedAccessToken;
        }

        private async Task<string> CreateAccessToken()
        {
            using var formUrlEncodedContent = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("grant_type", "client_id") });
            using var authResponse = await HttpClient.PostAsync("auth/v1/token", formUrlEncodedContent);
            authResponse.EnsureSuccessStatusCode();
            
            await using var responseStream = await authResponse.Content.ReadAsStreamAsync();
            var jsonDocument = await JsonDocument.ParseAsync(responseStream);
            return jsonDocument.RootElement.GetProperty("access_token").GetString();
        }
        
        private async Task<string> CreateAccessToken(string username, string password)
        {
            using var formUrlEncodedContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password),
                new KeyValuePair<string, string>("scope", "offline_access"),
            });
            
            using var authResponse = await HttpClient.PostAsync("auth/v1/token", formUrlEncodedContent);
            authResponse.EnsureSuccessStatusCode();
            
            await using var responseStream = await authResponse.Content.ReadAsStreamAsync();
            var jsonDocument = await JsonDocument.ParseAsync(responseStream);
            return jsonDocument.RootElement.GetProperty("access_token").GetString();
        }
    }
}