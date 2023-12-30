namespace WasariEnvironment.Finders;

public interface IEnvironmentFeatureFinder
{
    Task<ICollection<EnvironmentFeature>> GetFeaturesAsync();
}