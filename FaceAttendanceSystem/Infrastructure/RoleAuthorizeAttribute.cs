using System;
using System.Linq;
using System.Web.Mvc;

namespace FaceAttendanceSystem.Infrastructure
{
    /// <summary>
    /// Authorize filter that distinguishes the two failure cases:
    /// not signed in -> login redirect (or 401 for AJAX); signed in with the wrong role -> 403 page.
    /// Used globally with no roles (any signed-in user) and per controller with roles.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
    public class RoleAuthorizeAttribute : AuthorizeAttribute
    {
        public RoleAuthorizeAttribute()
        {
        }

        public RoleAuthorizeAttribute(params string[] roles)
        {
            Roles = string.Join(",", roles ?? Enumerable.Empty<string>());
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            var http = filterContext.HttpContext;

            if (http.User != null && http.User.Identity.IsAuthenticated)
            {
                filterContext.Result = ErrorResults.Forbidden(http);
                return;
            }

            if (http.Request.IsAjaxRequest())
            {
                http.Response.SuppressFormsAuthenticationRedirect = true;
                http.Response.TrySkipIisCustomErrors = true;
                filterContext.Result = new HttpStatusCodeResult(401, "Sign-in required");
                return;
            }

            base.HandleUnauthorizedRequest(filterContext);
        }
    }
}
