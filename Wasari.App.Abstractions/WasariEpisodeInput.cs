namespace Wasari.App.Abstractions;

public record WasariEpisodeInput(string Url, string Language, InputType Type) : IWasariEpisodeInput;