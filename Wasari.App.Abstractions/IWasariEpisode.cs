namespace Wasari.App.Abstractions;

public interface IWasariEpisode : IWasariBasicInfo
{
    int? Number { get; }

    TimeSpan? Duration { get; }

    string? SeriesName { get; }

    string Prefix => $"S{SeasonNumber:00}E{Number:00}";

    Func<IServiceProvider, Task<ICollection<IWasariEpisodeInput>>> InputsFactory { get; }
}