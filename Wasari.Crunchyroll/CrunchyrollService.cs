using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Flurl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using Wasari.Abstractions;
using Wasari.Crunchyroll.Abstractions;
using Wasari.Crunchyroll.API;
using Wasari.Crunchyroll.Models;
using Wasari.Puppeteer;

namespace Wasari.Crunchyroll
{
    public class CrunchyrollService : ISeriesProvider<CrunchyrollSeasonsInfo>
    {
        public CrunchyrollService(ILogger<CrunchyrollService> logger, BrowserFactory browserFactory, CrunchyrollApiServiceFactory crunchyrollApiServiceFactory, IServiceProvider serviceProvider)
        {
            Logger = logger;
            BrowserFactory = browserFactory;
            CrunchyrollApiServiceFactory = crunchyrollApiServiceFactory;
            ServiceProvider = serviceProvider;
        }

        private ILogger<CrunchyrollService> Logger { get; }

        private BrowserFactory BrowserFactory { get; }
        
        private CrunchyrollApiServiceFactory CrunchyrollApiServiceFactory { get; }
        
        private IServiceProvider ServiceProvider { get; }

        private async IAsyncEnumerable<CrunchyrollSeasonsInfo> GetSeasonsInfo(Page seriesPage,
            IReadOnlyDictionary<string, ApiEpisode> episodesDictionary, string seriesId)
        {
            var crunchyrollService = CrunchyrollApiServiceFactory.GetService();
            
            var seasons = await crunchyrollService.GetSeasons(seriesId).ToDictionaryAsync(i => i.Id);
            var episodes = await GetEpisodes(seriesPage, episodesDictionary).OrderBy(i => i.Number).ToArrayAsync();

            foreach (var episodesGroupedBySeason in episodes.GroupBy(i => i.SeasonId))
            {
                var season = seasons[episodesGroupedBySeason.Key];
                
                if(season.IsDubbed)
                    continue;
                
                var seasonsInfo = new CrunchyrollSeasonsInfo
                {
                    Season = season.Number,
                    Id = season.Id,
                    Title = season.Title,
                    Episodes = new List<CrunchyrollEpisodeInfo>()
                };

                foreach (var crunchyrollEpisodeInfo in episodesGroupedBySeason.Select((i, index) =>
                             new CrunchyrollEpisodeInfo
                             {
                                 Id = i.Id,
                                 Name = i.Name,
                                 Special = i.Special,
                                 Url = i.Url,
                                 ThumbnailId = i.ThumbnailId,
                                 Number = i.Number?.ToString("00"),
                                 SequenceNumber = i.Number ?? index,
                                 SeasonInfo = seasonsInfo,
                                 Premium = i.Premium
                             })) seasonsInfo.Episodes.Add(crunchyrollEpisodeInfo);

                yield return seasonsInfo;
            }
        }

        public async Task<ISeriesInfo<CrunchyrollSeasonsInfo>> GetSeries(string seriesUrl)
        {
            var crunchyrollService = CrunchyrollApiServiceFactory.GetService();
            var browser = await BrowserFactory.GetBrowserAsync();
            
            Logger.LogDebug("Creating browser tab...");
            await using var seriesPage = await browser.NewPageAsync();
            Logger.LogDebug("Navigating to {@Url}", seriesUrl);
            await seriesPage.GoToAsync(seriesUrl);

            if (seriesPage.Url.Contains("beta."))
            {
                var betaService = ServiceProvider.GetService<BetaCrunchyrollService>();
                return await betaService!.GetSeries(seriesPage.Url);
            }

            Logger.LogInformation("Parsing series page {@Url}", seriesUrl);
            await using var titleHandle =
                await seriesPage.WaitForXPathAsync("//*[@id=\"showview-content-header\"]/div[@class='ch-left']/h1");

            if (titleHandle == null)
                throw new Exception("Failed to find title handle. Is this a series URL?");

            var name = await titleHandle.GetPropertyValue<string>("innerText");

            Logger.LogDebug("Parsing seasons for series {@SeriesName}", name);

            await seriesPage.WaitForXPathAsync("//*[@id=\"sidebar_elements\"]/li[1]/div");
            var id = await seriesPage.EvaluateExpressionAsync<string>(
                "JSON.parse(document.getElementsByClassName(\"show-actions\")[0]?.attributes['data-contentmedia'].value).mediaId");
            var episodesDictionary = await crunchyrollService.GetAllEpisodes(id)
                .ToDictionaryAsync(i => i.ThumbnailIds.Single());

            var seasons = await GetSeasonsInfo(seriesPage, episodesDictionary, id).ToListAsync();
            var specialSeason = CreateSpecialSeason(seasons);

            if (specialSeason.Episodes.Any())
                seasons.Add(specialSeason);

            return new CrunchyrollSeriesInfo
            {
                Name = name,
                Seasons = seasons.OrderBy(i => i.Season).ToArray(),
                Id = id
            };
        }

        private static CrunchyrollSeasonsInfo CreateSpecialSeason(List<CrunchyrollSeasonsInfo> seasons)
        {
            var specialSeason = new CrunchyrollSeasonsInfo
            {
                Episodes = new List<CrunchyrollEpisodeInfo>(),
                Season = 0,
                Title = "Specials"
            };

            foreach (var seasonInfo in seasons)
            {
                if (seasonInfo.Episodes.Any(o => o.Special))
                {
                    var specialEpisodes = seasonInfo.Episodes.Where(i => i.Special).OrderBy(i => i.Number).ToArray();

                    foreach (var specialEpisode in specialEpisodes)
                    {
                        var currentEpisode = specialSeason.Episodes.Any()
                            ? specialSeason.Episodes.Max(o => o.SequenceNumber)
                            : -1;
                        var newEpisodeNumber = currentEpisode + 1;
                        var convertedSpecialEpisode = new CrunchyrollEpisodeInfo
                        {
                            Id = specialEpisode.Id,
                            Name = specialEpisode.Name,
                            Number = newEpisodeNumber.ToString("00"),
                            Special = true,
                            Url = specialEpisode.Url,
                            SeasonInfo = specialSeason,
                            SequenceNumber = newEpisodeNumber,
                            ThumbnailId = specialEpisode.ThumbnailId,
                            Premium = specialEpisode.Premium
                        };

                        specialSeason.Episodes.Add(convertedSpecialEpisode);
                        seasonInfo.Episodes.Remove(specialEpisode);
                    }
                }
            }

            return specialSeason;
        }

        private async IAsyncEnumerable<RawCrunchyrollEpisode> GetEpisodes(Page pageHandle,
            IReadOnlyDictionary<string, ApiEpisode> episodesDictionary)
        {
            var episodesHandles =
                await pageHandle.XPathAsync("//a[@class='portrait-element block-link titlefix episode']");

            foreach (var episodeHandle in episodesHandles)
            {
                await using var imageHandle = await episodeHandle.SingleOrDefaultXPathAsync("./img");
                await using var srcHandle = await imageHandle.SingleOrDefaultXPathAsync("./@data-thumbnailurl") ??
                                            await imageHandle.SingleOrDefaultXPathAsync("./@src");
                var srcValue = await srcHandle.GetPropertyValue<string>("value");
                var thumbnailUrl = Url.ParsePathSegments(srcValue).LastOrDefault();
                var thumbnailId = thumbnailUrl?[..32];
                var apiEpisode = episodesDictionary.GetValueOrDefault(thumbnailId);
                var url = await episodeHandle.GetPropertyValue<string>("href");

                if (apiEpisode == null)
                {
                    Logger.LogWarning("Failed to match episode to API. {@Url}", url);
                    continue;
                }
                
                yield return new RawCrunchyrollEpisode
                {
                    Id = apiEpisode.Id,
                    Name = apiEpisode.Title,
                    Special = !apiEpisode.EpisodeNumber.HasValue,
                    Url = url,
                    SeasonId = apiEpisode.SeasonId,
                    SeasonNumber = apiEpisode.SeasonNumber,
                    Number = apiEpisode.EpisodeNumber ?? apiEpisode.SequenceNumber,
                    Premium = apiEpisode.IsPremium
                };
            }
        }
    }
}