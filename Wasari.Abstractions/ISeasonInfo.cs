using System.Collections.Generic;

namespace Wasari.Abstractions
{
    public interface ISeasonInfo
    {
        int Season { get; }
        
        string Title { get; }
        
        bool Dubbed { get; }
        
        string DubbedLanguage { get; }
    }
}