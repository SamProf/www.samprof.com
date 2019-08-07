using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using SamProf.Web.Core;

namespace SamProf.Web.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }


        public BlogPost[] Posts;

        public async Task OnGet()
        {
            var postsPath = Path.Combine(StaticSiteGenerator.Instance.BasePath, "_posts");
            Posts = new DirectoryInfo(postsPath).GetFiles("*.md").Select(e =>
            {
                var year = e.Name.Substring(0, 4);
                var month = e.Name.Substring(5, 2);
                var day = e.Name.Substring(8, 2);
                var name = Path.GetFileNameWithoutExtension(e.Name).Substring(11);
                return new BlogPost()
                {
                    Path = e.Name,
                    Year = year,
                    Month = month, 
                    Day = day,
                    Name = name,

                };
            }).ToArray();
        }

        public async Task OnPost()
        {
            await StaticSiteGenerator.Instance.Generate(Request);
            await OnGet();
        }
    }




    public class BlogPost
    {
        public string Path { get; set; }
        public string Year { get; set; }
        public string Month { get; set; }
        public string Day { get; set; }
        public string Name { get; set; }
    }

}
