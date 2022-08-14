using System.Collections.Generic;

namespace Wasari.Abstractions
{
    public interface IEpisodeInfo
    {
        string Id { get; }
        
        ISeasonInfo SeasonInfo { get; }
        
        ISeriesInfo SeriesInfo { get; }
        
        bool? Dubbed { get; }
        
        string? DubbedLanguage { get; }

        string Name { get; }

        string Number { get; }

        decimal SequenceNumber { get; }

        bool Special { get; }

        ICollection<EpisodeInfoVideoSource> Sources { get; }

        string FilePrefix { get; }
    }
}