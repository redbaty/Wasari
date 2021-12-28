using Microsoft.Extensions.Options;

namespace WasariEnvironment;

public class EnvironmentService
{
    public EnvironmentService(IOptions<EnvironmentOptions> options)
    {
        Options = options;
    }

    private IOptions<EnvironmentOptions> Options { get; }

    private IEnumerable<EnvironmentFeatureType> ExistingFeatures(params EnvironmentFeatureType[] features)
    {
        if (Options.Value.Features == null)
            yield break;

        foreach (var environmentFeature in features)
        {
            if (Options.Value.Features.Select(i => i.Type).Contains(environmentFeature))
                yield return environmentFeature;
        }
    }

    public EnvironmentFeature? GetFeature(EnvironmentFeatureType type) =>
        Options.Value.Features?.SingleOrDefault(i => i.Type == type);
    
    public EnvironmentFeature GetFeatureOrThrow(EnvironmentFeatureType type) =>
        GetFeature(type) ?? throw new MissingEnvironmentFeatureException(new []{type});

    public IEnumerable<EnvironmentFeatureType> GetMissingFeatures(params EnvironmentFeatureType[] features)
    {
        var availableFeatures = ExistingFeatures(features).ToHashSet();
        return features.Where(requiredFeature => !availableFeatures.Contains(requiredFeature));
    }

    public Version? GetModuleVersion(EnvironmentFeatureType type, string module)
    {
        var environmentFeature = Options.Value.Features?.SingleOrDefault(i => i.Type == type);
        var featureModule = environmentFeature?.Modules?.SingleOrDefault(i => i.Name == module);
        return featureModule?.Version;
    }

    public bool IsFeatureMissing(EnvironmentFeatureType featureType) => GetMissingFeatures(featureType).Any();
    
    public bool IsFeatureAvailable(EnvironmentFeatureType featureType) => !GetMissingFeatures(featureType).Any();

    public void ThrowIfFeatureNotAvailable(params EnvironmentFeatureType[] features)
    {
        var missingFeatures = GetMissingFeatures(features).ToArray();

        if (missingFeatures.Length > 0)
            throw new MissingEnvironmentFeatureException(missingFeatures);
    }
}