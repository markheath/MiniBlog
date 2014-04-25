using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace BloggerImporter
{
    class AtomFeedImporter
    {
        public static List<AtomPost> ImportFromFile(string path)
        {
            var xml = File.ReadAllText(path);
            var root = XElement.Parse(xml);

            // key bits of the atom xml format:
            // <feed>
            //    <category /> - can be many
            //    <title /> = title of blog
            //    <author><name /></author>
            //    <entry>
            //       <id/>
            //       <category scheme='http://schemas.google.com/g/2005#kind' term='http://schemas.google.com/blogger/2008/kind#post'/>
            //       <category scheme='http://www.blogger.com/atom/ns#' term='A Category'/>
            //       <published/> formatted like this: 2007-02-01T14:01:23.326Z
            //       <updated/>
            //       <title/>
            //       <content type='html'/>
            //       <author><name /></author>
            //    <entry>

            // dratfs:
            // <app:control xmlns:app="http://purl.org/atom/app#">
            //  <app:draft>yes</app:draft>
            // </app:control>

            XNamespace atom = "http://www.w3.org/2005/Atom";
            XNamespace app = "http://purl.org/atom/app#";
            XNamespace thr = "http://purl.org/syndication/thread/1.0";
            //var count = root.Descendants(atom + "entry").Count();

            var posts = (from e in root.Descendants(atom + "entry")
                where e.Elements(atom + "category").Any(x => x.Attribute("term").Value == "http://schemas.google.com/blogger/2008/kind#post")
                select new AtomPost
                       {
                           BloggerId = e.Element(atom + "id").Value,
                           Title = e.Element(atom + "title").Value,
                           Published = DateTime.Parse(e.Element(atom + "published").Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                           Updated = DateTime.Parse(e.Element(atom + "updated").Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                           Content = e.Element(atom + "content").Value,
                           Categories = (from t in e.Elements(atom + "category")
                               where t.Attribute("scheme").Value == "http://www.blogger.com/atom/ns#"
                               select t.Attribute("term").Value).ToList(),
                           IsDraft = e.Elements(app + "control").SelectMany(control => control.Elements(app + "draft").Select(draft => draft.Value)).Any(isDraft => isDraft == "yes"),
                           RepliesUrl = e.Elements(atom + "link")
                               .Where(x => x.Attribute("rel").Value == "replies" &&
                                           x.Attribute("type").Value == "application/atom+xml")
                               .Select(x => x.Attribute("href").Value)
                               .FirstOrDefault(),

                           PublishUrl = e.Elements(atom + "link")
                               .Where(x => x.Attribute("rel").Value == "alternate" &&
                                           x.Attribute("type").Value == "text/html")
                               .Select(x => x.Attribute("href").Value)
                               .FirstOrDefault(),
                           Comments = new List<AtomComment>()
                       }).ToList();

            //posts.Dump("Posts");

            var comments = root.Descendants(atom + "entry")
                .Where(e => e.Elements(atom + "category").Any(x => x.Attribute("term")
                    .Value == "http://schemas.google.com/blogger/2008/kind#comment"))
                .Select(e => new AtomComment()
                             {
                                 BloggerId = e.Element(atom + "id").Value,
                                 Title = e.Element(atom + "title").Value,
                                 Published = DateTime.Parse(e.Element(atom + "published").Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                                 Updated = DateTime.Parse(e.Element(atom + "updated").Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                                 Content = e.Element(atom + "content").Value,
                                 AuthorName = e.Element(atom + "author").Elements(atom + "name").Select(x => x.Value).FirstOrDefault(),
                                 AuthorUrl = e.Element(atom + "author").Elements(atom + "uri").Select(x => x.Value).FirstOrDefault(),
                                 AuthorEmail = e.Element(atom + "author").Elements(atom + "email").Select(x => x.Value).FirstOrDefault(),
                                 // could do author image				
                                 InReplyTo = e.Element(thr + "in-reply-to").Attribute("ref").Value,

                                 PublishUrl = e.Elements(atom + "link")
                                     .Where(x => x.Attribute("rel").Value == "alternate" &&
                                                 x.Attribute("type").Value == "text/html")
                                     .Select(x => x.Attribute("href").Value)
                                     .FirstOrDefault(),
                                 //Elements = e.Elements()
                             }).ToList();

            //comments.Dump("Comments");

            int repliesToComment = 0;
            int unmatched = 0;
            foreach (var comment in comments)
            {
                var replyToPost = posts.SingleOrDefault(p => p.BloggerId == comment.InReplyTo);
                if (replyToPost != null)
                {
                    replyToPost.Comments.Add(comment);
                }
                else
                {
                    var replyToComment = comments.Any(c => c.BloggerId == comment.InReplyTo);
                    if (replyToComment) repliesToComment++;
                    else unmatched++;
                }
            }

            Console.WriteLine("{0} replies to comment", repliesToComment);
            Console.WriteLine("{0} unmatched", unmatched);
            return posts;
        }
    }
}