using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
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
            var bloggerImportFile = @"C:\Users\mheath\Dropbox (Personal)\soundcode-blog-05-30-2014.xml";
            // var bloggerImportFile = @"c:\users\mark\downloads\sound-code-02-16-2014.xml";
            var postsPath = Path.GetFullPath(@"..\..\..\Website\posts");
            if (!Directory.Exists(postsPath))
            {
                throw new Exception("Posts folder not found");
            }
            
            var posts = AtomFeedImporter.ImportFromFile(bloggerImportFile);
            bool updated = false;
            foreach (var post in posts)
            {
                // use agility pack
                var doc = new HtmlDocument();
                doc.LoadHtml(post.Content);
                if (doc.DocumentNode == null)
                    throw new InvalidDataException("null document node");

                var imageNodes = doc.DocumentNode.SelectNodes("//img");
                if (imageNodes != null)
                {
                    updated = true;
                    ProcessImageNodes(imageNodes, post, postsPath);
                }

                var links = doc.DocumentNode.SelectNodes("//a");
                if (links != null)
                {
                    foreach (var linkNode in links)
                    {
                        var hrefAttribute = linkNode.Attributes["href"];
                        if (hrefAttribute != null)
                        {
                            var href = new Uri(hrefAttribute.Value, UriKind.RelativeOrAbsolute); //new Uri(linkNode.GetAttributeValue("href", "?"));
                            if (href.IsAbsoluteUri)
                            {
                                var authority = href.GetLeftPart(UriPartial.Authority);
                                if (authority.Contains("mark-dot-net.blogspot"))
                                {
                                    string newLink;
                                    if (hrefAttribute.Value.Contains("/search/label/"))
                                    {
                                        var cat = hrefAttribute.Value.Split('/').Last();
                                        newLink = "/category/" + cat.ToLower().Replace(' ', '+');
                                    }
                                    else
                                    {
                                        // assume a regular post
                                        newLink = "/post/" + Utils.GetSlugFromUrl(hrefAttribute.Value);
                                    }

                                    Console.WriteLine("Link to {0} fixing to {1}", href, newLink);
                                    hrefAttribute.Value = newLink;
                                    updated = true;
                                }
                            }
                        }
                    }
                }

                //images.Dump("PostImages");
                // TODO: update post content
                if (updated)
                {
                    var sb = new StringBuilder();
                    doc.Save(new StringWriter(sb));
                    post.Content = sb.ToString();
                    //Console.WriteLine(post.Content);
                }
            }


            var author = "Mark Heath";
            var dateFormat = "yyyy-MM-dd HH:mm:ss";
            
            foreach (var post in posts)
            {
                var postXml = new XElement("post",
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
                //Console.WriteLine(x.ToString());
                //break;

                var fileName = String.Format("{0}-{1}.xml", post.Published.ToString("yyyy-MM-dd"), post.GetSlug());
                File.WriteAllText(Path.Combine(postsPath, fileName), postXml.ToString());

            }
            Console.WriteLine("Finished importing, press any key...");
            Console.ReadKey();
        }

        private static void ProcessImageNodes(IEnumerable<HtmlNode> imageNodes, AtomPost post, string postsPath)
        {
            var postImageNumber = 0;
            foreach (var img in imageNodes)
            {
                postImageNumber++;

                var srcAttribute = img.Attributes["src"];
                var imageSource = srcAttribute.Value;
                var extension = VirtualPathUtility.GetExtension(new Uri(imageSource).GetLeftPart(UriPartial.Path));
                var newFileName = post.GetSlug() + "-" + postImageNumber + extension;

                var downloadPath = Path.Combine(postsPath, "files", newFileName);
                if (!File.Exists(downloadPath))
                {
                    Console.WriteLine("Downloading {0} to {1}", imageSource, newFileName);
                    DownloadImage(imageSource, downloadPath);
                }
                srcAttribute.Value = "/posts/files/" + newFileName;

                if (img.ParentNode.Name == "a")
                {
                    var imageRef = img.ParentNode.Attributes["href"];

                    if (imageRef != null && imageRef.Value.EndsWith(extension))
                    {
                        imageRef.Value = srcAttribute.Value;
                    }
                }
            }
        }

        private static void DownloadImage(string url, string outputPath)
        {
            try
            {
                PerformImageDownload(url, outputPath);

            }
            catch (Exception e)
            {
                Console.WriteLine("Error downloading image [{0}]", url);
                Console.WriteLine(e.Message);
            }
        }

        private static void PerformImageDownload(string url, string outputPath)
        {
            var request = WebRequest.Create(url);
            using (var response = request.GetResponse())
            using (var stream = response.GetResponseStream())
            {
                // string contentType = response.ContentType;
                // Download the file
                using (var file = File.OpenWrite(outputPath))
                {
                    var buffer = new byte[1024];
                    int bytesRead;
                    do
                    {
                        bytesRead = stream.Read(buffer, 0, buffer.Length);
                        file.Write(buffer, 0, bytesRead);
                    } while (bytesRead > 0);
                }
            }
        }
    }

    static class Utils
    {
        private static readonly Regex slugRegex = new Regex(@"/\d\d/(.*)\.html");

        public static string GetSlugFromUrl(string url)
        {
            return slugRegex.Match(url).Groups[1].Value;
        }

        public static string GetSlug(this AtomPost post)
        {
            return GetSlugFromUrl(post.PublishUrl);
        }
    }
}
