namespace Wasari.Cli
{
    public sealed class InvalidRangeException : Exception
    {
        public InvalidRangeException() : base("Invalid range sent. Please use format {starting}-{final} or {starting}- or -{final}")
        {
        }
    }
}