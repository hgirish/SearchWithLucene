using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.En;
using Lucene.Net.Analysis.Miscellaneous;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Search.Spell;
using Lucene.Net.Store;
using Lucene.Net.Util;

namespace SearchLib {
    public class MovieIndex : IDisposable {
        private const LuceneVersion MATCH_LUCENE_VERSION = LuceneVersion.LUCENE_48;
        private const int SNIPPET_LENGTH = 100;
        private readonly IndexWriter writer;
        private readonly Analyzer analyzer;
        private readonly QueryParser queryParser;
        private readonly SearcherManager searcherManager;
        IndexWriterConfig _indexWriterConfig;
        private string _indexPath;

        public MovieIndex(string indexPath)
        {
            if (!System.IO.Directory.Exists(indexPath)) {
                System.IO.Directory.CreateDirectory(indexPath);
            }
            _indexPath = indexPath;
            analyzer = SetupAnalyzer();
            queryParser = SetupQueryParser(analyzer);
            _indexWriterConfig = new IndexWriterConfig(MATCH_LUCENE_VERSION, analyzer) {
                OpenMode = OpenMode.CREATE_OR_APPEND
            };
            writer = new IndexWriter(FSDirectory.Open(indexPath),
               new IndexWriterConfig(MATCH_LUCENE_VERSION, analyzer) {
                   OpenMode = OpenMode.CREATE_OR_APPEND
               });
            searcherManager = new SearcherManager(writer, true, null);
        }

        private QueryParser SetupQueryParser(Analyzer analyzer)
        {
            return new MultiFieldQueryParser(
                MATCH_LUCENE_VERSION,
                new[] { "title", "description" },
                analyzer);
        }

        private Analyzer SetupAnalyzer()
        {

            return new EnhancedEnglishAnalyzer(MATCH_LUCENE_VERSION, EnglishAnalyzer.DefaultStopSet);
        }
        public void BuildIndex(IEnumerable<Movie> movies)
        {
            if (movies == null) {
                throw new ArgumentNullException();
            }

            foreach (var movie in movies) {
                Document movieDocument = BuildDocument(movie);
                writer.UpdateDocument(new Term("id", movie.MovieId.ToString()), movieDocument);
            }
            writer.Flush(true, true);
            writer.Commit();
        }

        private Document BuildDocument(Movie movie)
        {
            Document doc = new Document {
                new StoredField("movieid", movie.MovieId),
                new TextField("title", movie.Title, Field.Store.YES),
                new TextField("description", movie.Description, Field.Store.NO),
                new StoredField("snippet", MakeSnippet(movie.Description)),
                new StringField("rating", movie.Rating,Field.Store.YES)
            };
            return doc;
        }

        private string MakeSnippet(string description)
        {
            return (string.IsNullOrWhiteSpace(description) || description.Length <= SNIPPET_LENGTH)
                ? description
                : $"{description.Substring(0, SNIPPET_LENGTH)}...";
        }

        public SearchResults Search(string queryString)
        {
            int resultsPerPage = 10;
            List<FieldDefinition> fields = new List<FieldDefinition> {
                new FieldDefinition{Name="title", IsDefault = true },
                new FieldDefinition{Name="rating" },
                new FieldDefinition{Name="description"}
            };
           
            Query query = BuildQuery(queryString,fields);
            searcherManager.MaybeRefreshBlocking();
            IndexSearcher searcher = searcherManager.Acquire();

            try {
                TopDocs topDocs = searcher.Search(query, resultsPerPage);
                if (topDocs.TotalHits < 1) {
                    Console.WriteLine("No result found with TermQuery, Calling Prefix Query");
                    query = BuildQuery(queryString, fields,true);
                    topDocs = searcher.Search(query, resultsPerPage);
                }
                if (topDocs.TotalHits < 1) {
                    Console.WriteLine("No result found with PrefixQuery, Calling Fuzzy Query");
                    query = BuildQuery(queryString, fields, true,true);
                    topDocs = searcher.Search(query, resultsPerPage);
                }
                return CompileResults(searcher, topDocs);
            }
            finally {
                searcherManager.Release(searcher);
                searcher = null;
            }
    
        }

        private SearchResults CompileResults(IndexSearcher searcher, TopDocs topDocs)
        {
            SearchResults searchResults = new SearchResults {
                TotalHits = topDocs.TotalHits
            };
            foreach (var result in topDocs.ScoreDocs) {
                Document document = searcher.Doc(result.Doc);
                Hit searchResult = new Hit {
                    Rating = document.GetField("rating")?.GetStringValue(),
                    MovieId = document.GetField("movieid")?.GetStringValue(),
                    Score = result.Score,
                    Title = document.GetField("title")?.GetStringValue(),
                    Snippet = document.GetField("snippet")?.GetStringValue()
                };
                searchResults.Hits.Add(searchResult);
            }
            return searchResults;
        }

        private Query BuildQuery(string queryString)
        {
           
            return queryParser.Parse(queryString);
        }
        private Query BuildQuery(string userInput, IEnumerable<FieldDefinition> fields,bool prefixQuery = false, bool fuzzyQuery = false)
        {
            BooleanQuery query = new BooleanQuery();
            IList<string> tokens = Tokenize(userInput);

            //combine tokens present in user input
            if (tokens.Count > 1) {
                FieldDefinition defaultField = fields.FirstOrDefault(f => f.IsDefault == true);
                query.Add(BuildExactPhraseQuery(tokens, defaultField), Occur.SHOULD);

                foreach (var q in GetIncrementalMatchQuery(tokens, defaultField))
                    query.Add(q, Occur.SHOULD);
            }

            //create a term query per field - non boosted
            foreach (var token in tokens)
                foreach (var field in fields)
                    query.Add(new TermQuery(new Term(field.Name, token)), Occur.SHOULD);
            if (prefixQuery) {
                foreach (var  token in tokens) {
                    foreach (var field in fields) {
                        query.Add(new PrefixQuery(new Term(field.Name, token)), Occur.SHOULD);
                    }
                }
            }
            if (fuzzyQuery) {
                foreach (var token in tokens) {
                    foreach (var field in fields) {
                        query.Add(new FuzzyQuery(new Term(field.Name, token)), Occur.SHOULD);
                    }
                }
            }

            return query;
        }
        //private static Query BuildQuery(Analyzer analyzer, IEnumerable<string> searchFields, string searchQuery)
        //{
        //    var query = new BooleanQuery();

        //    var tokenStream = analyzer.TokenStream(null, new StringReader(searchQuery));
        //    var termAttribute = (TermAttribute)tokenStream.GetAttribute(typeof(TermAttribute));

        //    while (tokenStream.IncrementToken()) {
        //        var term = termAttribute.Term();
        //        var booleanQuery = new BooleanQuery();

        //        foreach (var searchField in searchFields) {
        //            // TermQuery, PrefixQuery or FuzzyQuery
        //            booleanQuery.Add(new TermQuery(new Term(searchField, term)), BooleanClause.Occur.SHOULD);
        //            booleanQuery.Add(new PrefixQuery(new Term(searchField, term)), BooleanClause.Occur.SHOULD);
        //            booleanQuery.Add(new FuzzyQuery(new Term(searchField, term)), BooleanClause.Occur.SHOULD);
        //        }

        //        query.Add(booleanQuery, BooleanClause.Occur.MUST);
        //    }

        //    return query;
        //}
        Query BuildExactPhraseQuery(IList<string> tokens, FieldDefinition field)
        {
            //boost factor (6) and slop (2) come from configuration - code omitted for simplicity
            PhraseQuery pq = new PhraseQuery() { Boost = tokens.Count * 6, Slop = 2 };
            foreach (var token in tokens)
                pq.Add(new Term(field.Name, token));

            return pq;
        }
        public string[] CheckSpelling(string userInput)
        {
            SingleInstanceLockFactory silf = new SingleInstanceLockFactory();
            var spellingDirectory = System.IO.Path.Combine(_indexPath, "Suggestions");
            if (!System.IO.Directory.Exists(spellingDirectory)) {
                System.IO.Directory.CreateDirectory(spellingDirectory);
            }

            RAMDirectory ramDirectory = new RAMDirectory();

            Lucene.Net.Store.Directory indexDirectory = FSDirectory.Open(_indexPath,silf);
            IndexWriterConfig conf = new IndexWriterConfig(MATCH_LUCENE_VERSION, analyzer) {
                OpenMode = OpenMode.CREATE_OR_APPEND
            };
            // IndexWriter iw = new IndexWriter(indexDirectory, conf);
            var reader = DirectoryReader.Open(indexDirectory);
            SpellChecker spellChecker = new SpellChecker(indexDirectory);
            spellChecker.IndexDictionary(new LuceneDictionary(reader,"title"),
                conf,false);
          var suggestions =  spellChecker.SuggestSimilar(userInput, 5);
            return suggestions;
            
        }
        IEnumerable<Query> GetIncrementalMatchQuery(IList<string> tokens, FieldDefinition field)
        {
            BooleanQuery bq = new BooleanQuery();
            foreach (var token in tokens)
                bq.Add(new TermQuery(new Term(field.Name, token)), Occur.SHOULD);

            //5 comes from config - code omitted
            int upperLimit = Math.Min(tokens.Count, 5);
            for (int match = 2; match <= upperLimit; match++) {
                BooleanQuery q = bq.Clone() as BooleanQuery;
                q.Boost = match * 3;
                q.MinimumNumberShouldMatch = match;
                yield return q;
            }
        }
        IList<string> Tokenize(string userInput)
        {
            List<string> tokens = new List<string>();
            using (var reader = new StringReader(userInput)) {
                using (TokenStream stream = analyzer.GetTokenStream("myfield", reader)) {
                    stream.Reset();
                    while (stream.IncrementToken()) {
                        tokens.Add(stream.GetAttribute<ICharTermAttribute>().ToString());
                    }
                }
            }
            return tokens;
        }

        protected  virtual  void Dispose(bool disposing)
        {
            if (disposing) {
                searcherManager?.Dispose();
                analyzer?.Dispose();
                writer?.Dispose();
            }


        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
