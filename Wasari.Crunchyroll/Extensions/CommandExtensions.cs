using System;
using System.Collections.Generic;
using System.Linq;
using CliWrap;
using CliWrap.EventStream;
using Microsoft.Extensions.Logging;
using Wasari.Crunchyroll.Abstractions;
using Wasari.Crunchyroll.API;

namespace Wasari.Crunchyroll.Extensions
{
    internal static class ApiExtensions
    {
        public static IEnumerable<CrunchyrollSeasonsInfo> ToSeasonsInfo(this IEnumerable<ApiSeason> apiSeasons,
            IEnumerable<ApiEpisode> apiEpisodes)
        {
            var episodeBySeason = apiEpisodes.ToLookup(i => i.SeasonId);

            foreach (var season in apiSeasons)
            {
                var seasonInfo = new CrunchyrollSeasonsInfo
                {
                    Id = season.Id,
                    Season = season.Number,
                    Title = season.Title,
                    Episodes = new List<CrunchyrollEpisodeInfo>()
                };

                foreach (var apiEpisode in episodeBySeason[season.Id])
                {
                    seasonInfo.Episodes.Add(new CrunchyrollEpisodeInfo
                    {
                        Id = apiEpisode.Id,
                        Name = apiEpisode.Title,
                        Special = !apiEpisode.EpisodeNumber.HasValue,
                        Url = apiEpisode.StreamLink,
                        ThumbnailId = null,
                        Number = (apiEpisode.EpisodeNumber ?? apiEpisode.SequenceNumber).ToString("00"),
                        SequenceNumber = apiEpisode.EpisodeNumber ?? apiEpisode.SequenceNumber,
                        SeasonInfo = seasonInfo,
                        Premium = apiEpisode.IsPremium
                    });
                }
                
                yield return seasonInfo;
            }
        }
    }

    internal static class CommandExtensions
    {
        public static CommandWithRetry WithRetryCount(this Command command, int retryCount, TimeSpan? timeOut = null,
            ILogger logger = null, Action<CommandEvent> onCommandEvent = null) => new(retryCount, command,
            timeOut ?? TimeSpan.FromSeconds(5), logger, onCommandEvent);

        public static CommandWithRetry WithLogger(this CommandWithRetry command, ILogger logger) =>
            new(command.RetryCount, command.Command, command.Timeout, logger, command.OnCommandEvent);

        public static CommandWithRetry WithCommandHandler(this CommandWithRetry command,
            Action<CommandEvent> onCommandEvent) => new(command.RetryCount, command.Command, command.Timeout,
            command.Logger, onCommandEvent);
    }
}