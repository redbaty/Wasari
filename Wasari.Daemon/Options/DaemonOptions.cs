namespace Wasari.Daemon.Options;

public record DaemonOptions
{
    private int? _maxConcurrentDownloads;
    public bool NotificationEnabled { get; set; }

    public bool RedisLockEnabled { get; set; }
    
    public bool CheckVideoIntegrityAfterDownload { get; set; }

    public int? MaxConcurrentDownloads
    {
        get => RedisLockEnabled ? _maxConcurrentDownloads : null;
        set => _maxConcurrentDownloads = value;
    }
}