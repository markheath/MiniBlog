using System;

namespace BloggerImporter
{
    internal class AtomComment
    {
        public string BloggerId { get; set; }
        public string Title { get; set; }
        public DateTime Published { get; set; }
        public DateTime Updated { get; set; }
        public string Content { get; set; }
        public string AuthorName { get; set; }
        public string AuthorUrl { get; set; }
        public string AuthorEmail { get; set; }
        public string InReplyTo { get; set; }
        public string PublishUrl { get; set; }
    }
}