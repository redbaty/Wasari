namespace Wasari.Daemon.Models;

public record CheckDirectoryVideoIntegrityRequest(string Directory, bool DeleteFileIfInvalid, bool IncludeSubdirectories);