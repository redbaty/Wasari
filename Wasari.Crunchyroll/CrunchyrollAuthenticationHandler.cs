using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
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
    private static readonly JwtSecurityTokenHandler JwtSecurityTokenHandler = new();

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
            new KeyValuePair<string, string>("scope", "offline_access")
        });

        using var authResponse = await AuthHttpClient.PostAsync("auth/v1/token", formUrlEncodedContent);
        authResponse.EnsureSuccessStatusCode();

        await using var responseStream = await authResponse.Content.ReadAsStreamAsync();
        var jsonDocument = await JsonDocument.ParseAsync(responseStream);
        return jsonDocument.RootElement.GetProperty("access_token").GetString();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(Options.Value.Token))
            try
            {
                var jwtSecurityToken = JwtSecurityTokenHandler.ReadJwtToken(Options.Value.Token);
                var localTime = jwtSecurityToken.ValidTo.ToLocalTime();

                if (localTime < DateTime.Now) Logger.LogWarning("Skipping 'WASARI_AUTH_TOKEN' since it has expired");
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed parsing crunchyroll token, resetting");
                Options.Value.Token = null;
            }

        if (string.IsNullOrEmpty(Options.Value.Token))
        {
            if (AuthenticationOptions.Value.HasCredentials)
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
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized) Options.Value.Token = null;

        return response;
    }
}