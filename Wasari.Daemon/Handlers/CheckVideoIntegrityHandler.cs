using System.Text;
using Microsoft.Extensions.Options;
using Wasari.App;
using Wasari.Daemon.Models;
using Wasari.Daemon.Options;
using Wasari.FFmpeg;

namespace Wasari.Daemon.Handlers;

public class CheckVideoIntegrityHandler
{
    public async ValueTask Handle(CheckVideoIntegrityRequest request, IOptions<DaemonOptions> daemonOptions, IServiceProvider serviceProvider, FFmpegService fFmpegService, ILogger<CheckVideoIntegrityHandler> logger)
    {
        if (File.Exists(request.Path) == false)
        {
            logger.LogWarning("File {Path} does not exist", request.Path);
            return;
        }

        var fileIsValid = await fFmpegService.CheckIfVideoStreamIsValid(request.Path);
        logger.LogInformation("File {Path} is {Status}", request.Path, fileIsValid ? "valid" : "invalid");

        if (fileIsValid) return;

        if (request.DeleteFileIfInvalid)
        {
            File.Delete(request.Path);
            logger.LogInformation("File {Path} was deleted", request.Path);
        }

        if (daemonOptions.Value.NotificationEnabled && serviceProvider.GetService<NotificationService>() is { } notificationService)
        {
            var fileName = Path.GetFileName(request.Path);

            var fileDirectory = Path.GetDirectoryName(request.Path);
            if (fileDirectory != null)
            {
                var dir = new DirectoryInfo(fileDirectory);

                if (dir.Parent?.Parent != null)
                {
                    var prefix = $"{dir.Parent.Parent.Name}/{dir.Parent.Name}";
                    fileName = $"{prefix}/{fileName}";
                }
            }

            var sb = new StringBuilder($"File {fileName} was corrupted");
            if (request.DeleteFileIfInvalid) sb.Append(" and was deleted");
            await notificationService.SendNotificationAsync(sb.ToString());
        }
    }
}