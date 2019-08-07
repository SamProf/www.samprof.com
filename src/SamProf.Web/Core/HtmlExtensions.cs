using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SamProf.Web.Core
{
    public static class HtmlExtensions
    {
        public static string StaticPage(this IUrlHelper url, string pageName)
        {
            var res = url.Page(pageName);
            StaticSiteGenerator.Instance.TrackUrl(url, res);
            return res;
        }

        public static string StaticPage(this IUrlHelper url, string pageName, object values)
        {
            var res = url.Page(pageName, values);
            StaticSiteGenerator.Instance.TrackUrl(url, res);
            return res;
        }
    }
}
