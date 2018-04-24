using System;
using System.Collections.Generic;
using System.Text;

namespace SearchLib
{
    public interface ISearchEngine {
        void BuildIndex();
        SearchResults Search(string query);
    }
}
