namespace WasariEnvironment;

public readonly record struct EnvironmentFeature(EnvironmentFeatureType Type, Version? Version, EnvironmentFeatureModule[]? Modules, string Path);