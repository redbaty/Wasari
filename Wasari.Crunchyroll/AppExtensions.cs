using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasari.App;
using Wasari.App.Abstractions;

namespace Wasari.Crunchyroll
{
    public class CrunchyrollAuthenticationOptions
    {
        public string Token { get; set; }
    }

    internal class CrunchyrollAuthenticationHandler : DelegatingHandler
    {
        public CrunchyrollAuthenticationHandler(IOptions<CrunchyrollAuthenticationOptions> options, HttpClient authHttpClient, IOptions<AuthenticationOptions> authenticationOptions)
        {
            Options = options;
            AuthHttpClient = authHttpClient;
            AuthenticationOptions = authenticationOptions;
        }

        private IOptions<CrunchyrollAuthenticationOptions> Options { get; }
        
        private IOptions<AuthenticationOptions> AuthenticationOptions { get; }

        private HttpClient AuthHttpClient { get; }

        private async Task<string> CreateAccessToken()
        {
            using var formUrlEncodedContent = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("grant_type", "client_id") });
            using var authResponse = await AuthHttpClient.PostAsync("auth/v1/token", formUrlEncodedContent);
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

            using var authResponse = await AuthHttpClient.PostAsync("auth/v1/token", formUrlEncodedContent);
            authResponse.EnsureSuccessStatusCode();

            await using var responseStream = await authResponse.Content.ReadAsStreamAsync();
            var jsonDocument = await JsonDocument.ParseAsync(responseStream);
            return jsonDocument.RootElement.GetProperty("access_token").GetString();
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(Options.Value.Token))
            {
                if (!string.IsNullOrEmpty(AuthenticationOptions.Value.Username) && !string.IsNullOrEmpty(AuthenticationOptions.Value.Password))
                {
                    Options.Value.Token = await CreateAccessToken(AuthenticationOptions.Value.Username, AuthenticationOptions.Value.Password);
                }
                else
                {
                    Options.Value.Token = await CreateAccessToken();
                }
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Options.Value.Token);
            return await base.SendAsync(request, cancellationToken);
        }
    }

    public static class AppExtensions
    {
        public static void AddCrunchyrollServices(this IServiceCollection serviceCollection)
        {
            var crunchyBaseAddres = new Uri("https://beta-api.crunchyroll.com/");
            serviceCollection.AddHttpClient<CrunchyrollAuthenticationHandler>(c =>
            {
                c.BaseAddress = crunchyBaseAddres;
                c.DefaultRequestHeaders.Add("Authorization",
                    "Basic a3ZvcGlzdXZ6Yy0teG96Y21kMXk6R21JSTExenVPVnRnTjdlSWZrSlpibzVuLTRHTlZ0cU8=");
            });
            serviceCollection.AddHttpClient<CrunchyrollApiService>(c => { c.BaseAddress = crunchyBaseAddres; })
                .AddHttpMessageHandler<CrunchyrollAuthenticationHandler>();
            serviceCollection.AddHostDownloader<CrunchyrollDownloadService>("beta.crunchyroll.com");
        }
    }
}