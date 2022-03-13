using Wasari.Abstractions;

namespace Wasari.App.Exceptions;

public class PremiumEpisodesException : Exception
{
    private IEpisodeInfo[] PremiumEpisodes { get; }

    public PremiumEpisodesException(IEpisodeInfo[] premiumEpisodes) : base($"Premium only episodes encountered, but no credentials were provided. {premiumEpisodes.Select(i => i.FilePrefix).DefaultIfEmpty().Aggregate((x, y) => $"{x}, {y}")}")
    {
        PremiumEpisodes = premiumEpisodes;
    }
}