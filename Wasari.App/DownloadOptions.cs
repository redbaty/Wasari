namespace Wasari.App;

public record Range(int? Minimum, int? Maximum);

public record DownloadOptions
{
    public string? OutputDirectory { get; set; }
    
    public bool IncludeDubs { get; set; }
    
    public bool IncludeSubs { get; set; }
    
    public bool SkipExistingFiles { get; set; }
    
    public Range? EpisodesRange { get; set; }
    
    public Range? SeasonsRange { get; set; }

    internal Dictionary<string, Type> Modifiers { get; set; } = new();
    
    public bool CreateSeriesFolder { get; set; }
    
    public bool CreateSeasonFolder { get; set; }
}