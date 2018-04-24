using System;
using System.Collections.Generic;
using System.Text;

namespace SearchLib
{
   public static  class Settings
    {
        public static string IndexLocation { get; set; } = @"..\\..\..\LuceneIndexes";
        public static string MovieJsonFile { get; set; } = "..\\..\\..\\movies.json";
    }
}
