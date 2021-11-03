using System;
using System.Collections.Generic;

namespace Wasari.Models
{
    public class SeasonInfo
    {
        private string _id;

        public string Id
        {
            get => _id;
            internal set
            {
                if (!string.IsNullOrEmpty(_id))
                    throw new InvalidOperationException("Trying to change season ID");
                
                _id = value;
            }
        }

        public int Season { get; init; }
        
        public string Title { get; init; }
        
        public ICollection<EpisodeInfo> Episodes { get; init; }
    }
}