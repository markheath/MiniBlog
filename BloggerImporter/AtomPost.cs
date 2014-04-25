using System;
using System.Collections.Generic;

namespace BloggerImporter
{
    class AtomPost
    {
        public string BloggerId { get; set; }
        public string Title { get; set; }
        public DateTime Published { get; set; }
        public DateTime Updated { get; set; }
        public string Content { get; set; }
        public List<string> Categories { get; set; }
        public bool IsDraft { get; set; }
        public string RepliesUrl { get; set; }
        public string PublishUrl { get; set; }
    }
}