using System.Collections.Generic;

namespace Wasari.Abstractions
{
    public class EpisodeInfoVideoSource
    {
        public IEpisodeInfo? Episode { get; init; }

        public string? Url { get; init; }
        
        public string? LocalPath { get; set; }

        public string? Language { get; init; }
    }

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