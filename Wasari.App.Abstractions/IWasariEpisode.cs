namespace Wasari.App.Abstractions;

public interface IWasariEpisode : IWasariBasicInfo
{
    TimeSpan? Duration { get; }
    
    Func<IServiceProvider, Task<ICollection<IWasariEpisodeInput>>> InputsFactory { get; }
}