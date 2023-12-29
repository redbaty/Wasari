namespace Wasari.FFmpeg;

public class MultipleEncodersException : Exception
{
    public MultipleEncodersException(string message) : base(message)
    {
    }
}