namespace Wasari.Abstractions
{
    public interface IEpisodeInfo
    {
        ISeasonInfo SeasonInfo { get; }
        string Name { get; }
        string Number { get; }
        decimal SequenceNumber { get; }
        bool Special { get; }
        string FilePrefix { get; }
    }
}