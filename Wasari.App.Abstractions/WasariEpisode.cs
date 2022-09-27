namespace Wasari.App.Abstractions;

public record WasariEpisode(string? Title, string? SeriesName, int? SeasonNumber, int? Number, int? AbsoluteNumber, Func<IServiceProvider, Task<ICollection<IWasariEpisodeInput>>> InputsFactory, TimeSpan? Duration) : IWasariEpisode;