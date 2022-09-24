namespace Wasari.App.Abstractions;

public record WasariEpisodeInput(string Url, string Language, InputType Type) : IWasariEpisodeInput;

public record WasariEpisodeInputWithStream(string Url, string Language, InputType Type, int? AudioIndex, int? VideoIndex) : IWasariEpisodeInputStreamSelector;