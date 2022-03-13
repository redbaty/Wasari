using System;
using System.Collections.Generic;
using System.IO;
using Wasari.Abstractions;
using Wasari.Abstractions.Extensions;

namespace Wasari.Crunchyroll;

public class YoutubeDlEpisodeResult
{
    public IEpisodeInfo Episode { get; init; }
        
    public ICollection<YoutubeDlResult> Results { get; init; }
}