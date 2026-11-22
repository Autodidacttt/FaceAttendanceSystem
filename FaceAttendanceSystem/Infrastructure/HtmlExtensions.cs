using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Html;

namespace FaceAttendanceSystem.Infrastructure
{
    public static class HtmlExtensions
    {
        /// <summary>Bootstrap nav link, marked active when the current controller matches.</summary>
        public static IHtmlString NavLink(this HtmlHelper html, string text, string action, string controller)
        {
            var current = html.ViewContext.RouteData.GetRequiredString("controller");
            var active = string.Equals(current, controller, StringComparison.OrdinalIgnoreCase);
            var attributes = new System.Collections.Generic.Dictionary<string, object>
            {
                { "class", "nav-link" + (active ? " active" : "") }
            };
            if (active) attributes["aria-current"] = "page";
            var link = html.ActionLink(text, action, controller, null, attributes);
            return new HtmlString("<li class=\"nav-item\">" + link + "</li>");
        }

        /// <summary>Bootstrap badge colour for an attendance percentage (below 75% is a warning).</summary>
        public static string PercentClass(double percent)
        {
            return percent < 75 ? "text-danger fw-semibold" : "text-success";
        }
    }
}
