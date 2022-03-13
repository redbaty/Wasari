using Wasari.Abstractions;

namespace Wasari.App;

internal class DummySeasonInfo : ISeasonInfo
{
    public int Season { get; init; }
    
    public string? Title { get; init; }
    
    public bool Dubbed { get; init; }
    
    public string? DubbedLanguage { get; init; }
}