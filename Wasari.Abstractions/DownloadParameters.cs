namespace Wasari.Abstractions
{
    public class DownloadParameters
    {
        public string? OutputDirectory { get; init; }

        public string? SubtitleLanguage { get; init; }

        public string? CookieFilePath { get; init; }

        public bool UseHevc { get; init; }
        
        public bool UseAnime4K { get; init; }
        
        public string? Format { get; init; }

        public string? ConversionPreset { get; init; }

        public bool UseNvidiaAcceleration { get; init; } = true;

        public bool DeleteTemporaryFiles { get; init; } = true;

        public string? TemporaryDirectory { get; init; }
        
        public bool Subtitles { get; set; }
        
        public int ParallelDownloads { get; init; }
        
        public int ParallelMerging { get; init; }
        
        public bool CreateSeasonFolder { get; set; }

        public string? FileMask { get; set; }
    }
}