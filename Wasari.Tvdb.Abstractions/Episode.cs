namespace Wasari.Tvdb.Abstractions;

public record Episode(string Name, int? SeasonNumber, int? Number, bool IsMovie, string? Prefix);