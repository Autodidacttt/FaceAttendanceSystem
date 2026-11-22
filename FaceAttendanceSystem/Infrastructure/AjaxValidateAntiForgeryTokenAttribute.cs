using System;
using System.Web.Helpers;
using System.Web.Mvc;

namespace FaceAttendanceSystem.Infrastructure
{
    /// <summary>
    /// Anti-forgery validation for AJAX/JSON posts. The page renders @Html.AntiForgeryToken();
    /// scripts send that value in the "RequestVerificationToken" header and the browser sends the cookie.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class AjaxValidateAntiForgeryTokenAttribute : FilterAttribute, IAuthorizationFilter
    {
        public const string HeaderName = "RequestVerificationToken";

        public void OnAuthorization(AuthorizationContext filterContext)
        {
            if (filterContext == null) throw new ArgumentNullException(nameof(filterContext));

            var request = filterContext.HttpContext.Request;
            var cookie = request.Cookies[AntiForgeryConfig.CookieName];
            var headerToken = request.Headers[HeaderName];

            try
            {
                AntiForgery.Validate(cookie?.Value, headerToken);
            }
            catch (HttpAntiForgeryException)
            {
                filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;
                filterContext.HttpContext.Response.StatusCode = 400;
                filterContext.Result = new JsonResult { Data = new { error = "invalid_antiforgery_token" } };
            }
        }
    }
}
