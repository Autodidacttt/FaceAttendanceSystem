using System.Net;
using System.Web;
using System.Web.Mvc;

namespace FaceAttendanceSystem.Infrastructure
{
    /// <summary>Friendly 403/404 results that keep the correct HTTP status code (no redirect).</summary>
    public static class ErrorResults
    {
        public static ActionResult Forbidden(HttpContextBase context)
        {
            return Build(context, HttpStatusCode.Forbidden, "~/Views/Error/Forbidden.cshtml", "forbidden");
        }

        public static ActionResult NotFound(HttpContextBase context)
        {
            return Build(context, HttpStatusCode.NotFound, "~/Views/Error/NotFound.cshtml", "not_found");
        }

        private static ActionResult Build(HttpContextBase context, HttpStatusCode code, string view, string jsonError)
        {
            context.Response.StatusCode = (int)code;
            context.Response.TrySkipIisCustomErrors = true;

            if (context.Request.IsAjaxRequest())
            {
                return new JsonResult
                {
                    Data = new { error = jsonError },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }

            return new ViewResult { ViewName = view };
        }
    }
}
