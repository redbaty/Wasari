using Wasari.App.Abstractions;

namespace Wasari.App;

public record WasariEpisode(string Title, int SeasonNumber, int AbsoluteNumber, ICollection<IWasariEpisodeInput> Inputs, TimeSpan? Duration) : IWasariEpisode;