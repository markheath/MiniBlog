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

            var posts = AtomFeedImporter.ImportFromFile(bloggerImportFile);
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
                        post.Categories.Select(t => new XElement("category", t))),
                    new XElement("comments",
                        post.Comments.Where(c => c.InReplyTo == post.BloggerId)
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
            Console.ReadKey();
        }
        
    }
}
