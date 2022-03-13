using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Flurl;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using TomLonghurst.EnumerableAsyncProcessor.Extensions;
using Wasari.Abstractions;
using Wasari.Crunchyroll.Abstractions;
using Wasari.Crunchyroll.API;
using Wasari.Crunchyroll.Models;
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

        private async IAsyncEnumerable<CrunchyrollSeasonsInfo> GetSeasonsInfo(Browser browser, ISeriesInfo seriesInfo, Page seriesPage,
            IReadOnlyDictionary<string, ApiEpisode> episodesDictionary, string seriesId)
        {
            var crunchyrollService = CrunchyrollApiServiceFactory.GetService();
            
            var seasons = await crunchyrollService.GetSeasons(seriesId).ToDictionaryAsync(i => i.Id);
            var episodes = await GetEpisodes(browser, seriesPage, episodesDictionary, seasonId => seasons[seasonId].Number).OrderBy(i => i.Number).ToArrayAsync();

            foreach (var episodesGroupedBySeason in episodes.GroupBy(i => i.SeasonId))
            {
                var season = seasons[episodesGroupedBySeason.Key];
                
                var seasonsInfo = new CrunchyrollSeasonsInfo
                {
                    Season = season.Number,
                    Id = season.Id,
                    Title = season.Title,
                    Episodes = new List<IEpisodeInfo>()
                };

                foreach (var crunchyrollEpisodeInfo in episodesGroupedBySeason.Select((i, index) =>
                         {
                             var apiEpisode = episodesDictionary[i.Id];

                             var episode = new CrunchyrollEpisodeInfo
                             {
                                 Id = i.Id,
                                 Name = i.Name,
                                 Special = i.Special,
                                 Url = i.Url,
                                 ThumbnailId = i.ThumbnailId,
                                 Number = i.Number?.ToString("00"),
                                 SequenceNumber = i.Number ?? index,
                                 SeasonInfo = seasonsInfo,
                                 Premium = i.Premium,
                                 Dubbed = apiEpisode.IsDubbed,
                                 DubbedLanguage = apiEpisode.AudioLocale,
                                 SeriesInfo = seriesInfo
                             };
                             
                             episode.Sources.Add(new EpisodeInfoVideoSource
                             {
                                 Episode = episode,
                                 Language = apiEpisode.AudioLocale,
                                 Url = i.Url
                             });
                             
                             return episode;
                         })) seasonsInfo.Episodes.Add(crunchyrollEpisodeInfo);

                yield return seasonsInfo;
            }
        }

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
            var id = await seriesPage.EvaluateExpressionAsync<string>(
                "document.getElementsByClassName(\"show-actions\")[0]?.attributes['data-contentmedia'].value.match(/\"mediaId\":\"(?<id>\\w+)\"/).groups.id");
            
            Logger.LogInformation("Media ID found {@Id}", id);

            var seriesInfo = new CrunchyrollSeriesInfo
            {
                Name = name,
                Id = id
            };
            
            var episodesDictionary = await crunchyrollService.GetAllEpisodes(id)
                .ToDictionaryAsync(i => i.Id);
            
            var seasons = await GetSeasonsInfo(browser, seriesInfo, seriesPage, episodesDictionary, id).ToListAsync();
            var specialSeason = CreateSpecialSeason(seasons);
            
            if (specialSeason.Episodes.Any())
                seasons.Add(specialSeason);
            
            foreach (var season in seasons.OrderBy(i => i.Season))
            {
                foreach (var episode in season.Episodes)
                {
                    yield return episode;
                }
            }
        }

        private static CrunchyrollSeasonsInfo CreateSpecialSeason(List<CrunchyrollSeasonsInfo> seasons)
        {
            var specialSeason = new CrunchyrollSeasonsInfo
            {
                Episodes = new List<IEpisodeInfo>(),
                Season = 0,
                Title = "Specials"
            };

            foreach (var seasonInfo in seasons)
            {
                if (seasonInfo.Episodes.Any(o => o.Special))
                {
                    var specialEpisodes = seasonInfo.Episodes.Where(i => i.Special).OrderBy(i => i.Number).ToArray();

                    foreach (var specialEpisode in specialEpisodes.OfType<CrunchyrollEpisodeInfo>())
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

        private async Task<string> GetEpisodeId(Browser browser, string url)
        {
            var page = await browser.NewPageAsync();
            await page.GoToAsync(url);

            await page.WaitForExpressionAsync("document.getElementsByClassName('boxcontents')[0]");
            var id = await page.EvaluateExpressionAsync<string>("document.getElementsByClassName('boxcontents')[0]?.id?.split('_')[2]");

            await page.CloseAsync();
            return id;
        }

        private async IAsyncEnumerable<RawCrunchyrollEpisode> GetEpisodes(Browser browser, Page pageHandle,
            IReadOnlyDictionary<string, ApiEpisode> episodesDictionary, Func<string, int> seasonNumber)
        {
            var episodesHandles =
                await pageHandle.XPathAsync("//a[@class='portrait-element block-link titlefix episode']");

            var episodes = await episodesHandles
                .ToAsyncProcessorBuilder()
                .SelectAsync(async episodeHandle => await GetEpisode(browser, episodesDictionary, episodeHandle, seasonNumber))
                .ProcessInParallel(3);

            foreach (var episode in episodes)
            {
                yield return episode;
            }
        }

        private async Task<RawCrunchyrollEpisode> GetEpisode(Browser browser, IReadOnlyDictionary<string, ApiEpisode> episodesDictionary, ElementHandle episodeHandle, Func<string, int> seasonNumber)
        {
            await using var imageHandle = await episodeHandle.SingleOrDefaultXPathAsync("./img");
            await using var srcHandle = await imageHandle.SingleOrDefaultXPathAsync("./@data-thumbnailurl") ??
                                        await imageHandle.SingleOrDefaultXPathAsync("./@src");
            var srcValue = await srcHandle.GetPropertyValue<string>("value");
            var thumbnailUrl = Url.ParsePathSegments(srcValue).LastOrDefault();
            var thumbnailId = thumbnailUrl?[..32];
            var url = await episodeHandle.GetPropertyValue<string>("href");
            var episodeId = await GetEpisodeId(browser, url);
            var apiEpisode = episodesDictionary.GetValueOrDefault(episodeId);
            
            if (apiEpisode == null)
            {
                Logger.LogWarning("Failed to match episode to API. {@Url}", url);
                return null;
            }

            return new RawCrunchyrollEpisode
            {
                Id = apiEpisode.Id,
                Name = apiEpisode.Title,
                Special = !apiEpisode.EpisodeNumber.HasValue,
                Url = url,
                SeasonId = apiEpisode.SeasonId,
                SeasonNumber = seasonNumber.Invoke(apiEpisode.SeasonId),
                Number = apiEpisode.EpisodeNumber ?? apiEpisode.SequenceNumber,
                Premium = apiEpisode.IsPremium,
                ThumbnailId = thumbnailId
            };
        }
    }
}