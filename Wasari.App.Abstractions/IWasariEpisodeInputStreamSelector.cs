namespace Wasari.App.Abstractions;

public interface IWasariEpisodeInputStreamSelector : IWasariEpisodeInput
{
    int? AudioIndex { get; }
    
    int? VideoIndex { get; }
}