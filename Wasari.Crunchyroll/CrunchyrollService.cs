using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using TomLonghurst.EnumerableAsyncProcessor.Extensions;
using Wasari.Abstractions;
using Wasari.Crunchyroll.Abstractions;
using Wasari.Crunchyroll.API;
using Wasari.Crunchyroll.Extensions;
using Wasari.Puppeteer;

namespace Wasari.Crunchyroll
{
    public class CrunchyrollService : ISeriesProvider
    {
        public CrunchyrollService(ILogger<CrunchyrollService> logger, CrunchyrollApiServiceFactory crunchyrollApiServiceFactory, BrowserFactory browserFactory)
        {
            Logger = logger;
            CrunchyrollApiServiceFactory = crunchyrollApiServiceFactory;
            BrowserFactory = browserFactory;
        }

        private ILogger<CrunchyrollService> Logger { get; }

        private BrowserFactory BrowserFactory { get; }
        
        private CrunchyrollApiServiceFactory CrunchyrollApiServiceFactory { get; }

        public async IAsyncEnumerable<IEpisodeInfo> GetEpisodes(string seriesUrl)
        {
            var crunchyrollService = CrunchyrollApiServiceFactory.GetService();
            var browser = await BrowserFactory.GetBrowserAsync();
            
            Logger.LogDebug("Creating browser tab...");
            await using var seriesPage = await browser.NewPageAsync();
            Logger.LogDebug("Navigating to {@Url}", seriesUrl);
            await seriesPage.GoToAsync(seriesUrl);
            
            if (seriesPage.Url.Contains("beta."))
            {
                Logger.LogError("BETA Series with legacy link detected, please provide the beta link instead of the old one");
                throw new InvalidOperationException("BETA Series with legacy link detected, please provide the beta link instead of the old one");
            }
            
            Logger.LogInformation("Parsing series page {@Url}", seriesUrl);
            await using var titleHandle =
                await seriesPage.WaitForXPathAsync("//*[@id=\"showview-content-header\"]/div[@class='ch-left']/h1");
            
            if (titleHandle == null)
                throw new Exception("Failed to find title handle. Is this a series URL?");
            
            var name = await titleHandle.GetPropertyValue<string>("innerText");
            
            Logger.LogDebug("Parsing seasons for series {@SeriesName}", name);
            
            await seriesPage.WaitForXPathAsync("//*[@id=\"sidebar_elements\"]/li[1]/div");
            var seriesId = await seriesPage.EvaluateExpressionAsync<string>(
                "document.getElementsByClassName(\"show-actions\")[0]?.attributes['data-contentmedia'].value.match(/\"mediaId\":\"(?<id>\\w+)\"/).groups.id");
            
            var info = await crunchyrollService.GetSeriesInformation(seriesId);

            var seasons = await crunchyrollService
                .GetSeasons(seriesId)
                .ToArrayAsync();

            var episodes = await seasons
                .ToAsyncProcessorBuilder()
                .SelectAsync(season => crunchyrollService.GetEpisodes(season.Id).ToArrayAsync().AsTask())
                .ProcessInParallel(3);

            var seasonsInfo = seasons.ToSeasonsInfo(episodes.SelectMany(o => o), new CrunchyrollSeriesInfo
            {
                Id = seriesId,
                Name = info.Title
            }).ToArray();

            foreach (var episodeInfo in seasonsInfo.SelectMany(o => o.Episodes))
            {
                yield return episodeInfo;
            }
        }
    }
}