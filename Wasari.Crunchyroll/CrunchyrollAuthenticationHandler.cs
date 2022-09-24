using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wasari.App.Abstractions;

namespace Wasari.Crunchyroll;

internal class CrunchyrollAuthenticationHandler : DelegatingHandler
{
    public CrunchyrollAuthenticationHandler(IOptions<CrunchyrollAuthenticationOptions> options, HttpClient authHttpClient, IOptions<AuthenticationOptions> authenticationOptions, ILogger<CrunchyrollAuthenticationHandler> logger)
    {
        Options = options;
        AuthHttpClient = authHttpClient;
        AuthenticationOptions = authenticationOptions;
        Logger = logger;
    }

    private IOptions<CrunchyrollAuthenticationOptions> Options { get; }
        
    private IOptions<AuthenticationOptions> AuthenticationOptions { get; }
    
    private ILogger<CrunchyrollAuthenticationHandler> Logger { get; }

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
            if (Environment.GetEnvironmentVariable("WASARI_AUTH_TOKEN") is { } envToken && !string.IsNullOrEmpty(envToken))
            {
                Options.Value.Token = envToken;
                Logger.LogInformation("Authenticated to crunchyroll using environment token");
            }
            else if (!string.IsNullOrEmpty(AuthenticationOptions.Value.Username) && !string.IsNullOrEmpty(AuthenticationOptions.Value.Password))
            {
                Options.Value.Token = await CreateAccessToken(AuthenticationOptions.Value.Username, AuthenticationOptions.Value.Password);
                Logger.LogInformation("Authenticated to crunchyroll using username/password");
            }
            else
            {
                Options.Value.Token = await CreateAccessToken();
                Logger.LogWarning("Authenticated to crunchyroll using no authentication, this might lead to incomplete results");
            }
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Options.Value.Token);
        return await base.SendAsync(request, cancellationToken);
    }
}