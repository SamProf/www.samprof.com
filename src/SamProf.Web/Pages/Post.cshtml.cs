using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Markdig;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SamProf.Web.Core;

namespace SamProf.Web.Pages
{
    public class PostModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public string Year { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Month { get; set; }


        [BindProperty(SupportsGet = true)]
        public string Day { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Name { get; set; }

        public string PostText { get; private set; }


        public void OnGet()
        {
            var filePath = Path.Combine(StaticSiteGenerator.Instance.BasePath, "_posts", $"{Year}-{Month}-{Day}-{Name}.md");
            var fileContent = System.IO.File.ReadAllText(filePath);
            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();
            PostText = Markdown.ToHtml(fileContent, pipeline);

        }
    }
}