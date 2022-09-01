namespace Wasari.Cli
{
    public sealed class InvalidEpisodeRangeException : Exception
    {
        public InvalidEpisodeRangeException() : base("Invalid episode range sent. Please use format {startingEpisode}-{finalEpisode}")
        {
        }
    }
}