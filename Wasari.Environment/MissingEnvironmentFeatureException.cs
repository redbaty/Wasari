namespace WasariEnvironment;

internal sealed class MissingEnvironmentFeatureException : Exception
{
    public MissingEnvironmentFeatureException(ICollection<EnvironmentFeature> features) : base(
        $"One or more environment features are missing. ({features.Select(i => i.ToString()).Aggregate((x, y) => $"{x},{y}")})")
    {
        Data.Add(nameof(features), features);
    }
}