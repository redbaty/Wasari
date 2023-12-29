namespace Wasari.Tvdb;

public class MissingEnvironmentVariableException : Exception
{
    public MissingEnvironmentVariableException(string variableName) : base($"Missing environment variable: {variableName}")
    {
    }
}