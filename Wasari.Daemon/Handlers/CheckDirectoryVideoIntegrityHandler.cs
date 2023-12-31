using Wasari.Daemon.Models;
using Wolverine;

namespace Wasari.Daemon.Handlers;

public class CheckDirectoryVideoIntegrityHandler
{
    public async ValueTask Handle(CheckDirectoryVideoIntegrityRequest request, ILogger<CheckVideoIntegrityHandler> logger, IMessageBus messageBus)
    {
        if (Directory.Exists(request.Directory) == false)
        {
            logger.LogWarning("Directory {Directory} does not exist", request.Directory);
            return;
        }

        var files = Directory.GetFiles(request.Directory, "*.mkv", request.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        foreach (var file in files) await messageBus.PublishAsync(new CheckVideoIntegrityRequest(file, request.DeleteFileIfInvalid));
    }
}