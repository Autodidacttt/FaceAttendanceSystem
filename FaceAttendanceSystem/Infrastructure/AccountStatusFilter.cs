using System;
using System.Linq;
using System.Web.Mvc;
using System.Web.Routing;
using FaceAttendanceSystem.Data;

namespace FaceAttendanceSystem.Infrastructure
{
    /// <summary>
    /// Global filter that checks the signed-in account against the database on every MVC request:
    /// deactivated / deleted accounts, or accounts whose role changed, are signed out immediately.
    /// (Password changes are voluntary; nobody is forced to the change-password page.)
    /// </summary>
    public class AccountStatusFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var http = filterContext.HttpContext;
            var principal = http.User as AppPrincipal;
            if (principal == null) return;

            var controller = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
            if (string.Equals(controller, "Error", StringComparison.OrdinalIgnoreCase)) return;

            using (var db = new AppDbContext())
            {
                var status = db.Users
                    .Where(u => u.Id == principal.UserId)
                    .Select(u => new { u.IsActive, u.Role })
                    .FirstOrDefault();

                if (status == null || !status.IsActive || status.Role != principal.Role)
                {
                    AuthCookie.SignOut(http);
                    filterContext.Result = Respond(filterContext, 401, "Account", "Login");
                }
            }
        }

        private static ActionResult Respond(ActionExecutingContext ctx, int ajaxStatus, string controller, string action)
        {
            if (ctx.HttpContext.Request.IsAjaxRequest())
            {
                ctx.HttpContext.Response.SuppressFormsAuthenticationRedirect = true;
                ctx.HttpContext.Response.TrySkipIisCustomErrors = true;
                return new HttpStatusCodeResult(ajaxStatus);
            }

            return new RedirectToRouteResult(new RouteValueDictionary { { "controller", controller }, { "action", action } });
        }
    }
}
