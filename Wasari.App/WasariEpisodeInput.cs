using Wasari.App.Abstractions;

namespace Wasari.App;

public record WasariEpisodeInput(string Url, string Language, InputType Type) : IWasariEpisodeInput;