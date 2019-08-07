using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace SamProf.Web.Core
{
    public class StaticSiteGenerator
    {
        public static StaticSiteGenerator Instance { get; } = new StaticSiteGenerator();
        public List<string> Urls = new List<string>();
        public const string Header = "StaticSiteGenerator";

        public string BasePath { get; private set; }


        public StaticSiteGenerator()
        {
            BasePath = Path.GetFullPath(Path.Combine("./", "..", ".."));

        }

        public void TrackUrl(IUrlHelper urlHelper, string url)
        {
            if (urlHelper.ActionContext.HttpContext.Request.Headers.ContainsKey(Header))
            {
                if (!Urls.Contains(url))
                {
                    Urls.Add(url);
                }
            }
        }

        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs);
                }
            }
        }

        public async Task Generate(HttpRequest request)
        {
            var docsFolder = Path.Combine(BasePath, "docs");
            Directory.Delete(docsFolder, true);
            Directory.CreateDirectory(docsFolder);
            DirectoryCopy(Path.GetFullPath(Path.Combine("./", "wwwroot")), docsFolder, true);

            var httpClient = new HttpClient()
            {
                DefaultRequestHeaders =
                {
                    {Header, "true"}
                }
            };
            var baseUrl = $"{request.Scheme}://{request.Host.Value}";

            Urls = new List<string>() {"/"};

            for (int i = 0; i < Urls.Count; i++)
            {
                var url = Urls[i];
                Console.WriteLine(url);
                var uri = new Uri(new Uri(baseUrl), url);
                var html = await httpClient.GetStringAsync(uri);
                var name = url.Replace("/", "\\");
                var filePath = docsFolder + name;
                if (!filePath.EndsWith("\\"))
                {
                    filePath += "\\";
                }

                filePath += "Index";
                filePath += ".html";
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, html);
            }
        }
    }
}