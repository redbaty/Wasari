using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Wasari.Tvdb.Models;

namespace Wasari.Tvdb;

public class TvdbTokenHandler : DelegatingHandler
{
    private const string TvdbTokenCacheKey = "tvdb_token";
    private static readonly JwtSecurityTokenHandler JwtSecurityTokenHandler = new();

    public TvdbTokenHandler(IMemoryCache memoryCache, HttpClient tvdbClient)
    {
        MemoryCache = memoryCache;
        TvdbClient = tvdbClient;
    }

    private IMemoryCache MemoryCache { get; }

    private HttpClient TvdbClient { get; }

    private async Task<string> GetToken(ICacheEntry e, CancellationToken cancellationToken)
    {
        var response = await TvdbClient.PostAsJsonAsync("login", new
        {
            apikey = Environment.GetEnvironmentVariable("TVDB_API_KEY") ?? throw new MissingEnvironmentVariableException("TVDB_API_KEY"),
            pin = Environment.GetEnvironmentVariable("TVDB_API_PIN") ?? "TVDB_API_KEY"
        }, cancellationToken);

        response.EnsureSuccessStatusCode();

        var tokenResponse = await response.Content.ReadFromJsonAsync<TvdbResponse<TvdbTokenResponseData>>(cancellationToken);

        if (tokenResponse is not { Status: "success" }) throw new Exception("Failed to get token");

        var jwt = JwtSecurityTokenHandler.ReadJwtToken(tokenResponse.Data.Token);
        e.SetAbsoluteExpiration(jwt.ValidTo);

        return tokenResponse.Data.Token;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization == null)
        {
            var token = await MemoryCache.GetOrCreateAsync(TvdbTokenCacheKey, e => GetToken(e, cancellationToken));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var httpResponseMessage = await base.SendAsync(request, cancellationToken);

        if (httpResponseMessage.StatusCode == HttpStatusCode.Unauthorized) MemoryCache.Remove(TvdbTokenCacheKey);

        return httpResponseMessage;
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }
}