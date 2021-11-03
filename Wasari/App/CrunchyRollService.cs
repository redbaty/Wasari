using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crunchyroll.API;
using Crunchyroll.API.Models;
using Flurl;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using Wasari.Extensions;
using Wasari.Models;

namespace Wasari.App
{
    public class CrunchyRollService
    {
        public CrunchyRollService(Browser browser, ILogger<CrunchyRollService> logger,
            CrunchyrollApiService crunchyrollApiService)
        {
            Browser = browser;
            Logger = logger;
            CrunchyrollApiService = crunchyrollApiService;
        }

        private ILogger<CrunchyRollService> Logger { get; }

        private Browser Browser { get; }

        private static string[] BannedKeywords = { "(Russian)", "Dub)" };

        private CrunchyrollApiService CrunchyrollApiService { get; }

        private async IAsyncEnumerable<SeasonInfo> GetSeasonsInfo(Page seriesPage,
            Dictionary<string, ApiEpisode> episodesDictionary)
        {
            var seasonsHandles = await seriesPage.XPathAsync("//ul[@class='list-of-seasons cf']/li/a");
            var seasonNumber = 0;

            Logger.LogDebug("{@Handles} seasons handles found", seasonsHandles.Length);

            if (seasonsHandles.Length != 0)
            {
                foreach (var seasonHandle in seasonsHandles.Reverse())
                {
                    await using var realSeasonHandle = await seasonHandle.SingleOrDefaultXPathAsync("./..");
                    var title = await seasonHandle.GetPropertyValue<string>("title");
                    var trimmedTitle = title.Trim();

                    if (BannedKeywords.Any(i => trimmedTitle.EndsWith(i)))
                    {
                        Logger.LogWarning("Ignoring season due to blacklisted words {@SeasonName}", trimmedTitle);
                        continue;
                    }

                    Logger.LogDebug("Returning season {@SeasonNumber} '{@SeasonTile}'", seasonNumber + 1, trimmedTitle);
                    var seasonsInfo = new SeasonInfo
                    {
                        Season = seasonNumber + 1,
                        Title = trimmedTitle,
                        Episodes = new List<EpisodeInfo>()
                    };

                    await foreach (var episodeInfo in GetEpisodes(realSeasonHandle, episodesDictionary, seasonsInfo))
                    {
                        seasonsInfo.Episodes.Add(episodeInfo);
                    }

                    yield return seasonsInfo;
                    seasonNumber++;
                }
            }
            else
            {
                Logger.LogDebug("No seasons found, returning a default one");
                yield return new SeasonInfo
                {
                    Season = 1,
                    Title = null,
                    Episodes = new List<EpisodeInfo>()
                };
            }
        }

        public async Task<SeriesInfo> GetSeriesInfo(string seriesUrl)
        {
            Logger.LogDebug("Creating browser tab...");
            await using var seriesPage = await Browser.NewPageAsync();
            Logger.LogDebug("Navigating to {@Url}", seriesUrl);
            await seriesPage.GoToAsync(seriesUrl);

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
            var episodesDictionary = await CrunchyrollApiService.GetAllEpisodes(id)
                .ToDictionaryAsync(i => i.ThumbnailIds.Single());

            var seasons = await GetSeasonsInfo(seriesPage, episodesDictionary).ToListAsync();
            var specialSeason = CreateSpecialSeason(seasons);

            if (specialSeason.Episodes.Any())
                seasons.Add(specialSeason);

            return new SeriesInfo
            {
                Name = name,
                Seasons = seasons,
                Id = id
            };
        }

        private static SeasonInfo CreateSpecialSeason(List<SeasonInfo> seasons)
        {
            var specialSeason = new SeasonInfo
            {
                Episodes = new List<EpisodeInfo>(),
                Season = 0,
                Title = "Specials"
            };

            foreach (var seasonInfo in seasons)
            {
                if (seasonInfo.Episodes.Any(o => o.Special))
                {
                    var specialEpisodes = seasonInfo.Episodes.Where(i => i.Special).ToArray();

                    foreach (var specialEpisode in specialEpisodes)
                    {
                        var currentEpisode = specialSeason.Episodes.Any() ? specialSeason.Episodes.Max(o => o.SequenceNumber) : -1;
                        var newEpisodeNumber = currentEpisode + 1;
                        var convertedSpecialEpisode = new EpisodeInfo
                        {
                            Id = specialEpisode.Id,
                            Name = specialEpisode.Name,
                            Number = newEpisodeNumber.ToString("00"),
                            Special = true,
                            Url = specialEpisode.Url,
                            SeasonInfo = specialSeason,
                            SequenceNumber = newEpisodeNumber,
                            ThumbnailId = specialEpisode.ThumbnailId
                        };
                        
                        specialSeason.Episodes.Add(convertedSpecialEpisode);
                        seasonInfo.Episodes.Remove(specialEpisode);
                    }
                }
            }

            return specialSeason;
        }

        private async IAsyncEnumerable<EpisodeInfo> GetEpisodes(ElementHandle seriesHandle,
            Dictionary<string, ApiEpisode> episodesDictionary, SeasonInfo seasonsInfo)
        {
            var episodesHandles =
                await seriesHandle.XPathAsync(".//a[@class='portrait-element block-link titlefix episode']");
            var lastEpisode = 0;

            foreach (var episodeHandle in episodesHandles.Reverse())
            {
                await using var seasonHandle = await episodeHandle.SingleOrDefaultXPathAsync("./../../../..");
                await using var seasonTitleHandle =
                    seasonHandle != null ? await seasonHandle.SingleOrDefaultXPathAsync("./a") : null;
                var seasonTitle = seasonTitleHandle != null
                    ? await seasonTitleHandle.GetPropertyValue<string>("title")
                    : string.Empty;
                await using var imageHandle = await episodeHandle.SingleOrDefaultXPathAsync("./img");
                await using var srcHandle = await imageHandle.SingleOrDefaultXPathAsync("./@data-thumbnailurl") ??
                                            await imageHandle.SingleOrDefaultXPathAsync("./@src");
                var srcValue = await srcHandle.GetPropertyValue<string>("value");
                var name = await imageHandle.GetPropertyValue<string>("alt");

                var thumbnailUrl = Url.ParsePathSegments(srcValue).LastOrDefault();
                var thumbnailId = thumbnailUrl?[..32];
                var apiEpisode = episodesDictionary.GetValueOrDefault(thumbnailId);

                if (apiEpisode != null && string.IsNullOrEmpty(seasonsInfo.Id))
                {
                    seasonsInfo.Id = apiEpisode.SeasonId;
                }

                var url = await episodeHandle.GetPropertyValue<string>("href");
                var episodeUrl = url[url.LastIndexOf("/", StringComparison.Ordinal)..];
                var episode = episodeUrl.Split('-').Skip(1).First();

                if (BannedKeywords.Any(i => seasonTitle.EndsWith(i)))
                {
                    Logger.LogWarning("Ignoring episode due to blacklisted words {@EpisodeName} {@SeasonTitle}", name,
                        seasonTitle);
                    continue;
                }

                var episodeNumber = int.TryParse(episode, out var parsedEpisodeNumber)
                    ? parsedEpisodeNumber
                    : lastEpisode;

                if (apiEpisode is { IsClip: true })
                    continue;

                yield return new EpisodeInfo
                {
                    Name = name,
                    Url = url,
                    SequenceNumber = apiEpisode?.SequenceNumber ?? episodeNumber,
                    ThumbnailId = thumbnailId,
                    Number = apiEpisode?.SequenceNumber.ToString("00") ?? episodeNumber.ToString("00"),
                    Id = apiEpisode?.Id,
                    SeasonInfo = seasonsInfo,
                    Special = !apiEpisode?.EpisodeNumber.HasValue ?? false
                };

                lastEpisode++;
            }
        }
    }
}