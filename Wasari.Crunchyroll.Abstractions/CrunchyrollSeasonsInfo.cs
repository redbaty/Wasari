using System;
using System.Collections.Generic;
using Wasari.Abstractions;

namespace Wasari.Crunchyroll.Abstractions
{
    public class CrunchyrollSeasonsInfo : ISeasonInfo 
    {
        private string _id;

        public string Id
        {
            get => _id;
            set
            {
                if (!string.IsNullOrEmpty(_id))
                    throw new InvalidOperationException("Trying to change season ID");
                
                _id = value;
            }
        }
        
        public int Season { get; init; }
        
        public string Title { get; init; }
        
        public bool Dubbed { get; init; }
        
        public bool Special { get; set; }

        public string DubbedLanguage { get; init; }

        public ICollection<IEpisodeInfo> Episodes { get; init; }
    }
}