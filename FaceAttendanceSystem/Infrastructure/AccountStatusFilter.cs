using System;
using System.Linq;
using System.Web.Mvc;
using System.Web.Routing;
using FaceAttendanceSystem.Data;

namespace FaceAttendanceSystem.Infrastructure
{
    /// <summary>
    /// Global filter that checks the signed-in account against the database on every MVC request:
    /// - deactivated / deleted accounts, or accounts whose role changed, are signed out;
    /// - accounts with MustChangePassword are sent to the change-password page until they comply.
    /// </summary>
    public class AccountStatusFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var http = filterContext.HttpContext;
            var principal = http.User as AppPrincipal;
            if (principal == null) return;

            var controller = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
            var action = filterContext.ActionDescriptor.ActionName;
            if (string.Equals(controller, "Error", StringComparison.OrdinalIgnoreCase)) return;

            using (var db = new AppDbContext())
            {
                var status = db.Users
                    .Where(u => u.Id == principal.UserId)
                    .Select(u => new { u.IsActive, u.MustChangePassword, u.Role })
                    .FirstOrDefault();

                if (status == null || !status.IsActive || status.Role != principal.Role)
                {
                    AuthCookie.SignOut(http);
                    filterContext.Result = Respond(filterContext, 401, "Account", "Login");
                    return;
                }

                var isAccountAction = string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase) &&
                                      (string.Equals(action, "ChangePassword", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(action, "Logout", StringComparison.OrdinalIgnoreCase));

                if (status.MustChangePassword && !isAccountAction)
                {
                    filterContext.Result = Respond(filterContext, 403, "Account", "ChangePassword");
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
