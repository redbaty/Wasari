namespace Wasari.Daemon.Models;

public record CheckVideoIntegrityRequest(string Path, bool DeleteFileIfInvalid);