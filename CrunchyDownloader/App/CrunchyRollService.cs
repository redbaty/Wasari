using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CrunchyDownloader.Extensions;
using CrunchyDownloader.Models;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;

namespace CrunchyDownloader.App
{
    public class CrunchyRollService
    {
        public CrunchyRollService(Browser browser, ILogger<CrunchyRollService> logger)
        {
            Browser = browser;
            Logger = logger;
        }

        private ILogger<CrunchyRollService> Logger { get; }

        private Browser Browser { get; }

        private static string[] BannedKeywords = { "(Russian)", "Dub)" };

        private async IAsyncEnumerable<SeasonInfo> GetSeasonsInfo(Page seriesPage)
        {
            var seasonsHandles = await seriesPage.XPathAsync("//ul[@class='list-of-seasons cf']/li/a");
            var seasonNumber = 0;

            Logger.LogDebug("{@Handles} seasons handles found", seasonsHandles.Length);

            if (seasonsHandles.Length != 0)
            {
                foreach (var seasonHandle in seasonsHandles.Reverse())
                {
                    var title = await seasonHandle.GetPropertyValue<string>("title");
                    var trimmedTitle = title.Trim();

                    if (BannedKeywords.Any(i => trimmedTitle.EndsWith(i)))
                    {
                        Logger.LogWarning("Ignoring season due to blacklisted words {@SeasonName}", trimmedTitle);
                        continue;
                    }

                    Logger.LogDebug("Returning season {@SeasonNumber} '{@SeasonTile}'", seasonNumber + 1, trimmedTitle);
                    yield return new SeasonInfo
                    {
                        Season = seasonNumber + 1,
                        Title = trimmedTitle,
                        Episodes = new List<EpisodeInfo>()
                    };
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

            Logger.LogDebug("Parsing series page");
            var titleHandles =
                await seriesPage.XPathAsync("//*[@id=\"showview-content-header\"]/div[@class='ch-left']/h1");

            Logger.LogDebug("Found {@Handles} title handles", titleHandles.Length);

            await using var titleHandle = titleHandles.SingleOrDefault();

            if (titleHandle == null)
                throw new Exception("Failed to find title handle. Is this a series URL?");

            var name = await titleHandle.GetPropertyValue<string>("innerText");

            Logger.LogDebug("Parsing seasons for series {@SeriesName}", name);

            var seasons = await GetSeasonsInfo(seriesPage).ToArrayAsync();
            var seriesInfo = new SeriesInfo(name, seasons);
            await GetEpisodes(seriesPage, seriesInfo);

            return seriesInfo;
        }

        private async Task GetEpisodes(Page seriesPage, SeriesInfo seriesInfo)
        {
            var seasonsDictionary = seriesInfo.Seasons.ToDictionary(i => i.Title ?? string.Empty);

            var episodesHandles =
                await seriesPage.XPathAsync("//a[@class='portrait-element block-link titlefix episode']");
            foreach (var episodeHandle in episodesHandles)
            {
                await using var seasonHandle = await episodeHandle.SingleOrDefaultXPathAsync("./../../../..");
                await using var seasonTitleHandle =
                    seasonHandle != null ? await seasonHandle.SingleOrDefaultXPathAsync("./a") : null;
                var seasonTitle = seasonTitleHandle != null
                    ? await seasonTitleHandle.GetPropertyValue<string>("title")
                    : string.Empty;
                await using var imageHandle = await episodeHandle.SingleOrDefaultXPathAsync("./img");
                var name = await imageHandle.GetPropertyValue<string>("alt");
                var url = await episodeHandle.GetPropertyValue<string>("href");
                var episodeUrl = url[url.LastIndexOf("/", StringComparison.Ordinal)..];
                var episode = episodeUrl.Split('-').Skip(1).First();

                if (BannedKeywords.Any(i => seasonTitle.EndsWith(i)))
                {
                    Logger.LogWarning("Ignoring episode due to blacklisted words {@EpisodeName} {@SeasonTitle}", name,
                        seasonTitle);
                    continue;
                }

                var season = seasonsDictionary.GetValueOrDefault(seasonTitle);

                if (season != null)
                {
                    var episodeNumber = int.TryParse(episode, out var parsedEpisodeNumber)
                        ? parsedEpisodeNumber
                        : season.Episodes
                            .Select(i => i.Number)
                            .DefaultIfEmpty()
                            .Max() + 1;

                    season.Episodes.Add(new EpisodeInfo
                    {
                        Name = name,
                        Url = url,
                        SeasonInfo = season,
                        Number = episodeNumber
                    });
                }
            }
        }
    }
}