namespace WasariEnvironment;

public readonly record struct EnvironmentFeatureModule(string Name, Version? Version);

public readonly record struct EnvironmentFeature(EnvironmentFeatureType Type, Version? Version, EnvironmentFeatureModule[]? Modules, string Path);

public enum EnvironmentFeatureType
{
    YtDlp,
    Ffmpeg,
    FfmpegLibPlacebo,
    NvidiaGpu
}