namespace Wasari.App;

public record DownloadOptions
{
    public string? OutputDirectory { get; set; }
    
    public bool IncludeDubs { get; set; }
    
    public bool IncludeSubs { get; set; }
    
    public bool SkipExistingFiles { get; set; }
}