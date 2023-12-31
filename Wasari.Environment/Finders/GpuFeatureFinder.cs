using System.Diagnostics;
using LibreHardwareMonitor.Hardware;
using WasariEnvironment.Extensions;

namespace WasariEnvironment.Finders;

internal class GpuFeatureFinder : BaseFeatureFinder, IEnvironmentFeatureFinder
{
    public async Task<ICollection<EnvironmentFeature>> GetFeaturesAsync()
    {
        var computer = new Computer
        {
            IsGpuEnabled = true
        };

        try
        {
            var features = new HashSet<EnvironmentFeatureType>();

            computer.Open();

            foreach (var hardware in computer.Hardware)
                switch (hardware.HardwareType)
                {
                    case HardwareType.GpuNvidia:
                        if (await IsProgramAvailable("nvidia-smi", null).DefaultIfFailed())
                            features.Add(EnvironmentFeatureType.NvidiaGpu);

                        break;
                    case HardwareType.GpuAmd:
                        features.Add(EnvironmentFeatureType.AmdGpu);
                        break;
                }

            return features
                .Select(i => new EnvironmentFeature(i, null, null, string.Empty))
                .ToHashSet();
        }
        catch (Exception e)
        {
            Debug.WriteLine("Failed to open LibreHardwareMonitor: {0}", e);
            return Array.Empty<EnvironmentFeature>();
        }
        finally
        {
            computer.Close();
        }
    }
}