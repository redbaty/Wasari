namespace Wasari.Abstractions
{
    public interface ISeasonInfo
    {
        int Season { get; }
        
        string Title { get; }
        
        bool Dubbed { get; }
        
        bool Special { get; }
        
        string DubbedLanguage { get; }
    }
}