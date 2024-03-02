namespace Wasari.Tvdb;

internal class MissingEnvironmentVariableException : Exception
{
    public MissingEnvironmentVariableException(string variableName) : base($"Missing environment variable: {variableName}")
    {
    }
}