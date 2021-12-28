using System.Text;
using Wasari.Abstractions.Extensions;

namespace Wasari.ProgressSink;

public class ProgressBar
{
    internal struct Position {
        public int X;
        public int Y;
    }
    
    public int CurrentValue { get; set; }

    public int Max { get; set; }
    
    public string? Message { get; set; }
    
    internal Position? CurrentContainer { get; set; }

    internal string GenerateContent()
    {
        const int totalChunks = 30;

        var sb = new StringBuilder();
        sb.Append('[');
        sb.Append('░', totalChunks);
        sb.Append(']');
        
        var pctComplete = Convert.ToDouble(CurrentValue) / Max;
        int numChunksComplete = Convert.ToInt16(totalChunks * pctComplete);
        
        for (var i = 1; i <= numChunksComplete; i++) sb[i] = '█';

        var output = pctComplete.ToString("P");
        sb.Append(output.PadRight(15) + Message);
        return sb.ToString().Truncate(Console.WindowWidth - 1)!;
    }
}