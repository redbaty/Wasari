using CliFx.Extensibility;
using Microsoft.Extensions.Options;
using Wasari.FFmpeg;
using WasariEnvironment;

namespace Wasari.Cli.Converters;

public class ShaderConverter : BindingConverter<IFFmpegShader>
{
    public ShaderConverter(IOptions<FFmpegShaderPresets> shaderPresets, IServiceProvider serviceProvider, EnvironmentService environmentService)
    {
        ShaderPresets = shaderPresets;
        ServiceProvider = serviceProvider;
        EnvironmentService = environmentService;
    }

    private IOptions<FFmpegShaderPresets> ShaderPresets { get; }

    private IServiceProvider ServiceProvider { get; }

    private EnvironmentService EnvironmentService { get; }

    public override IFFmpegShader Convert(string? rawValue)
    {
        if (!EnvironmentService.IsFeatureAvailable(EnvironmentFeatureType.NvidiaGpu, EnvironmentFeatureType.FfmpegLibPlacebo)) throw new Exception("Using shaders requires an GPU and FFmpeg with libplacebo");

        if (!string.IsNullOrEmpty(rawValue))
        {
            if (File.Exists(rawValue))
                return new FileShader(new FileInfo(rawValue));

            if (ShaderPresets.Value.ShadersFactory.TryGetValue(rawValue, out var shaderFactory))
                return shaderFactory(ServiceProvider);
        }


        throw new Exception("Shader is neither a file nor a registered preset");
    }
}