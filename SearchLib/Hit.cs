namespace SearchLib {
    /// <summary>
    /// a representation of a movie item retrieved from lucene.net
    /// </summary>
    public class Hit {
        public string MovieId { get; set; }
        public string Title { get; set; }
        public string Snippet { get; set; }
        public string Rating { get; set; }
        public float Score { get; set; }
    }
}
