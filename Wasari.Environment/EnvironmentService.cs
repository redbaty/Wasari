using Microsoft.Extensions.Options;

namespace WasariEnvironment;

public class EnvironmentService
{
    public EnvironmentService(IOptions<EnvironmentOptions> options)
    {
        Options = options;
    }

    private IOptions<EnvironmentOptions> Options { get; }

    private IEnumerable<EnvironmentFeature> ExistingFeatures(params EnvironmentFeature[] features)
    {
        if (Options.Value.Features == null)
            yield break;

        foreach (var environmentFeature in features)
        {
            if (Options.Value.Features.Contains(environmentFeature))
                yield return environmentFeature;
        }
    }

    public IEnumerable<EnvironmentFeature> GetMissingFeatures(params EnvironmentFeature[] features)
    {
        var availableFeatures = ExistingFeatures(features).ToHashSet();
        return features.Where(requiredFeature => !availableFeatures.Contains(requiredFeature));
    }

    public bool IsFeatureMissing(EnvironmentFeature feature) => GetMissingFeatures(feature).Any();
    
    public bool IsFeatureAvailable(EnvironmentFeature feature) => !GetMissingFeatures(feature).Any();

    public void ThrowIfFeatureNotAvailable(params EnvironmentFeature[] features)
    {
        var missingFeatures = GetMissingFeatures(features).ToArray();

        if (missingFeatures.Length > 0)
            throw new MissingEnvironmentFeatureException(missingFeatures);
    }
}