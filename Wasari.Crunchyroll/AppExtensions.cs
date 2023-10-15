﻿using System;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Wasari.App.Extensions;

namespace Wasari.Crunchyroll;

public static class AppExtensions
{
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == HttpStatusCode.Unauthorized)
            .WaitAndRetryAsync(1, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2,
                retryAttempt)));
    }

    public static void AddCrunchyrollServices(this IServiceCollection serviceCollection)
    {
        var crunchyBaseAddres = new Uri("https://beta-api.crunchyroll.com/");
        serviceCollection.UseMinimalHttpLogger();
        serviceCollection.AddHttpClient<CrunchyrollAuthenticationHandler>(c =>
        {
            c.BaseAddress = crunchyBaseAddres;
            c.DefaultRequestHeaders.Add("Authorization",
                "Basic a3ZvcGlzdXZ6Yy0teG96Y21kMXk6R21JSTExenVPVnRnTjdlSWZrSlpibzVuLTRHTlZ0cU8=");
        });
        serviceCollection.AddHttpClient<CrunchyrollApiService>(c => c.BaseAddress = crunchyBaseAddres)
            .AddPolicyHandler(GetRetryPolicy())
            .AddHttpMessageHandler<CrunchyrollAuthenticationHandler>();
        serviceCollection.Configure<CrunchyrollAuthenticationOptions>(c => { c.Token = Environment.GetEnvironmentVariable("WASARI_AUTH_TOKEN"); });
        serviceCollection.AddHostDownloader<CrunchyrollDownloadService>("crunchyroll.com", "beta.crunchyroll.com", "www.crunchyroll.com");
    }
}