namespace Wasari.App.Abstractions;

public record WasariEpisodeInputWithStream(string Url, string Language, InputType Type, int? AudioIndex, int? VideoIndex) : IWasariEpisodeInputStreamSelector;