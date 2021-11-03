namespace Wasari.Models
{
    public class DownloadParameters
    {
        public string OutputDirectory { get; init; }

        public string SubtitleLanguage { get; init; }

        public string CookieFilePath { get; init; }

        public bool UseHevc { get; init; } = true;

        public string ConversionPreset { get; init; }

        public bool UseNvidiaAcceleration { get; init; } = true;

        public bool UseHardwareAcceleration { get; init; } = true;

        public bool DeleteTemporaryFiles { get; init; } = true;

        public string TemporaryDirectory { get; init; }
        
        public bool Subtitles { get; set; }
    }
}