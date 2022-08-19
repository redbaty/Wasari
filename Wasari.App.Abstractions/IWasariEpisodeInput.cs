namespace Wasari.App.Abstractions;

public interface IWasariEpisodeInput
{
    string Url { get; }
    
    string Language { get; }
    
    InputType Type { get; }
}