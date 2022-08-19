namespace Wasari.App.Abstractions;

public interface IWasariEpisode : IWasariBasicInfo
{
    TimeSpan? Duration { get; }
    
    ICollection<IWasariEpisodeInput> Inputs { get; }
}