using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HtmlAgilityPack;

namespace BloggerImporter
{
    class Program
    {
        static void Main(string[] args)
        {
            var bloggerImportFile = @"C:\Users\mheath\Dropbox\soundcode-blog-04-25-2014.xml";
            // var bloggerImportFile = @"c:\users\mark\downloads\sound-code-02-16-2014.xml";
            var xml = File.ReadAllText(bloggerImportFile);
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
                        select new
                        {
                            BloggerId = e.Element(atom + "id").Value,
                            Title = e.Element(atom + "title").Value,
                            Published = Convert.ToDateTime(e.Element(atom + "published").Value).ToUniversalTime(),
                            Updated = Convert.ToDateTime(e.Element(atom + "updated").Value),
                            Content = e.Element(atom + "content").Value,
                            // blogger categories are more like tags
                            Tags = from t in e.Elements(atom + "category")
                                   where t.Attribute("scheme").Value == "http://www.blogger.com/atom/ns#"
                                   select t.Attribute("term").Value,
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
                        }).ToList();

            //posts.Dump("Posts");

            var comments = root.Descendants(atom + "entry")
                     .Where(e => e.Elements(atom + "category").Any(x => x.Attribute("term")
                         .Value == "http://schemas.google.com/blogger/2008/kind#comment"))
                     .Select(e => new
                     {
                         BloggerId = e.Element(atom + "id").Value,
                         Title = e.Element(atom + "title").Value,
                         Published = Convert.ToDateTime(e.Element(atom + "published").Value).ToUniversalTime(),
                         Updated = Convert.ToDateTime(e.Element(atom + "updated").Value),
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

            int repliesToPost = 0;
            int repliesToComment = 0;
            int unmatched = 0;
            foreach (var comment in comments)
            {
                var replyToPost = posts.Any(p => p.BloggerId == comment.InReplyTo);
                if (!replyToPost)
                {
                    var replyToComment = comments.Any(c => c.BloggerId == comment.InReplyTo);
                    if (replyToComment) repliesToComment++;
                    else unmatched++;
                }
                else
                {
                    repliesToPost++;
                }
            }

            
            Console.WriteLine("{0} replies to post", repliesToPost);
            Console.WriteLine("{0} replies to comment", repliesToComment);
            Console.WriteLine("{0} unmatched", unmatched);
            
            int imageCount = 0;
            foreach (var post in posts)
            {
                // use agility pack
                var doc = new HtmlDocument();
                doc.LoadHtml(post.Content);
                try
                {
                    if (doc.DocumentNode == null)
                        throw new InvalidDataException("null document node");

                    var images = doc.DocumentNode.SelectNodes("//img")
                        .Select(img => img.GetAttributeValue("src", "?")).ToList();

                    imageCount += images.Count;
                    //images.Dump("PostImages");

                }
                catch (ArgumentNullException ne) // seems to throw this if no images present
                {
                    if (post.Content.Contains("<img"))
                        Console.WriteLine("PROBLEM: {0}", post.Content);
                }
                //.Count().Dump();
                //.Select(n => n.GetAttributeValue("src", "?")).Dump();
                //.Dump();
                //.Select(img => img.GetAttributeValue("src", "?")).ToList().Dump();
            }
            Console.WriteLine("{0} images found", imageCount);
            // TODO: fix-up references to my blog
            // TODO: fix-up images

            var author = "Mark Heath";
            var dateFormat = "yyyy-MM-dd HH:mm:ss";
            var slugRegex = new Regex(@"/\d\d/(.*)\.html");
            foreach (var post in posts)
            {
                var slug = slugRegex.Match(post.PublishUrl).Groups[1].Value;
                var x = new XElement("post",
                    new XElement("title", post.Title),
                    new XElement("slug", post.PublishUrl),
                    new XElement("author", author),
                    new XElement("pubDate", post.Published.ToString(dateFormat)),
                    new XElement("lastModified", post.Updated.ToString(dateFormat)),
                    new XElement("content", post.Content),
                    new XElement("ispublished", post.IsDraft ? "false" : "true"),
                    new XElement("categories",
                        post.Tags.Select(t => new XElement("category", t))),
                    new XElement("comments",
                        comments.Where(c => c.InReplyTo == post.BloggerId)
                        .Select(c => new XElement("comment",
                        new XAttribute("isAdmin", c.AuthorName == "mheath" ? "true" : "false"),
                        new XAttribute("isApproved", "true"),
                        new XAttribute("id", Guid.NewGuid().ToString()),
                              new XElement("author", c.AuthorName),
                              new XElement("email", c.AuthorEmail),
                              new XElement("website", c.AuthorUrl),
                              new XElement("ip"),
                              new XElement("userAgent"),
                              new XElement("date", c.Published.ToString(dateFormat)),
                              new XElement("content", c.Content)))
                        ));
                Console.WriteLine(x.ToString());
                break;
            }
        }
    }
}
