using System;
using System.Collections.Generic;
using System.Text;

namespace SearchLib {
    /// <summary>
    /// results after executing a lucene.net search on the movie index
    /// </summary>
    public class SearchResults {
        public SearchResults() => Hits = new List<Hit>();
        public string Time { get; set; }
        public int TotalHits { get; set; }
        public IList<Hit> Hits { get; set; }
    }
}
