using System;
using System.IO;
using SearchLib;

namespace Client
{
    class Program
    {
        static void Main(string[] args)
        {
            DeleteIndexFiles();
            SearchEngine engine = new SearchEngine();
            engine.BuildIndex();
            string userInput = string.Empty;
            Console.WriteLine("type quit to exit");
            do {
                Console.WriteLine("\nsearch:\\>");
                userInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(userInput)) {
                    break;
                }
                else if (userInput.Equals("quit")) {
                    break;
                }

                var results = engine.Search(userInput);
               
                DisplayResults(results);
                var suggestions = engine.Suggestions(userInput);
                Console.WriteLine("Suggestions:");
                foreach (var item in suggestions) {
                    Console.WriteLine($"\n{item}");
                }

            } while (true);
        }

        private static void DisplayResults(SearchResults results)
        {
            Console.WriteLine($"displaying {results.Hits.Count} of {results.TotalHits} results \n---");
            foreach (var result in results.Hits) {
                Console.WriteLine($"{result.Title}, {result.Rating} ({result.Score}) {(string.IsNullOrWhiteSpace(result.Snippet) ? "" : "\n" + result.Snippet)}\n");
            }
        }

        private static void DeleteIndexFiles()
        {
            if (!System.IO.Directory.Exists(Settings.IndexLocation)) {
                System.IO.Directory.CreateDirectory(Settings.IndexLocation);
            }
            foreach (FileInfo f in new DirectoryInfo(Settings.IndexLocation).GetFiles()) {
                f.Delete();
            }
        }
    }
}
