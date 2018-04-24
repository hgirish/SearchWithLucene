using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace SearchLib
{
   public class Repository
    {
        public static IEnumerable<Movie> GetMoviesFromFile()
        {
            var movies = JsonConvert.DeserializeObject<List<Movie>>(
                System.IO.File.ReadAllText(Settings.MovieJsonFile));
            return movies;
        }
    }
}
