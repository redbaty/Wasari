namespace Wasari.App.Exceptions;

public sealed class NoEpisodeFoundException : Exception
{
    public NoEpisodeFoundException() : base("No episodes found.")
    {
    }
}