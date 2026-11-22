using System.Web.Mvc;

namespace FaceAttendanceSystem.Controllers
{
    /// <summary>Targets for customErrors (used when an error escapes MVC, e.g. unknown URLs).</summary>
    [AllowAnonymous]
    public class ErrorController : Controller
    {
        public ActionResult Forbidden() => Status(403, "Forbidden");

        public ActionResult NotFound() => Status(404, "NotFound");

        public ActionResult ServerError() => Status(500, "ServerError");

        private ActionResult Status(int code, string view)
        {
            Response.StatusCode = code;
            Response.TrySkipIisCustomErrors = true;
            return View(view);
        }
    }
}
