using Wasari.Abstractions;

namespace Wasari.App.Exceptions;

public class PremiumEpisodesException : Exception
{
    public PremiumEpisodesException(IEnumerable<IEpisodeInfo> premiumEpisodes) : base($"Premium only episodes encountered, but no credentials were provided. {premiumEpisodes.Select(i => i.FilePrefix).DefaultIfEmpty().Aggregate((x, y) => $"{x}, {y}")}")
    {
    }
}