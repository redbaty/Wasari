namespace Wasari.App.Abstractions;

public interface IWasariEpisode : IWasariBasicInfo
{
    int Number { get; }
    
    TimeSpan? Duration { get; }
    
    Func<IServiceProvider, Task<ICollection<IWasariEpisodeInput>>> InputsFactory { get; }
}