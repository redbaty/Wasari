using Wasari.Abstractions;

namespace Wasari.App;

internal class DummyEpisodeInfo : IEpisodeInfo
{
    public string? Id { get; init; }

    public ISeasonInfo? SeasonInfo { get; init; }

    public ISeriesInfo? SeriesInfo { get; init; }

    public bool? Dubbed { get; init; }

    public string? DubbedLanguage { get; init; }

    public string? Name { get; init; }

    public string? Number { get; init; }

    public decimal SequenceNumber { get; init; }

    public bool Special { get; init; }

    public ICollection<EpisodeInfoVideoSource>? Sources { get; init; }

    public string? FilePrefix { get; init; }
}