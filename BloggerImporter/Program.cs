using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
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
            var postsPath = Path.GetFullPath(@"..\..\..\Website\posts");
            if (!Directory.Exists(postsPath))
            {
                throw new Exception("Posts folder not found");
            }
            
            var posts = AtomFeedImporter.ImportFromFile(bloggerImportFile);
            int imageCount = 0;
            foreach (var post in posts)
            {
                // use agility pack
                var doc = new HtmlDocument();
                doc.LoadHtml(post.Content);
                if (doc.DocumentNode == null)
                    throw new InvalidDataException("null document node");

                var nodes = doc.DocumentNode.SelectNodes("//img");
                if (nodes != null)
                {
                    var postImageNumber = 0;
                    foreach (var img in nodes)
                    {
                        postImageNumber++;
                        var imageSource = img.GetAttributeValue("src", "?");
                        imageCount++;
                        var extension = VirtualPathUtility.GetExtension(new Uri(imageSource).GetLeftPart(UriPartial.Path));
                        var newFileName = post.GetSlug() + "-" + postImageNumber + extension;

                        Console.WriteLine("Downloading {0} to {1}", imageSource, newFileName);
                        
                        // DownloadImage(imageSource, Path.Combine(postsPath, "files", newFileName));
                        img.SetAttributeValue("src", "/posts/files/" + newFileName);
                    }
                }
                
                //images.Dump("PostImages");
                // TODO: update post content
            }
            Console.WriteLine("{0} images found", imageCount);
            // TODO: fix-up references to my blog

            var author = "Mark Heath";
            var dateFormat = "yyyy-MM-dd HH:mm:ss";
            
            foreach (var post in posts)
            {
                var x = new XElement("post",
                    new XElement("title", post.Title),
                    new XElement("slug", post.GetSlug()),
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

        private static void DownloadImage(string url, string outputPath)
        {
            var request = WebRequest.Create(url);
            using (var response = request.GetResponse())
            using (var stream = response.GetResponseStream())
            {
                // string contentType = response.ContentType;
                // Download the file
                using (var file = File.OpenWrite(outputPath))
                {
                    // Remark: if the file is very big read it in chunks
                    // to avoid loading it into memory
                    var buffer = new byte[response.ContentLength];
                    var read = stream.Read(buffer, 0, buffer.Length);
                    file.Write(buffer, 0, read);
                }
            }
            
        }
        
    }

    static class ExtensionMethods
    {
        private static readonly Regex slugRegex = new Regex(@"/\d\d/(.*)\.html");
        public static string GetSlug(this AtomPost post)
        {
            return slugRegex.Match(post.PublishUrl).Groups[1].Value;
        }
    }
}
