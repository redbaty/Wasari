using System;

namespace Wasari.Crunchyroll;

internal sealed class PremiumEpisodeException : Exception
{
    public PremiumEpisodeException(int episodeNumber, int seasonNumber) : base("One or more episodes are premium-only")
    {
        Data.Add("EpisodeNumber", episodeNumber);
        Data.Add("SeasonNumber", seasonNumber);
    }
}