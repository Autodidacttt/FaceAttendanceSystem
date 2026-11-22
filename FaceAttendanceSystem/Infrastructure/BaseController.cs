using System.Linq;
using System.Web.Mvc;
using FaceAttendanceSystem.Data;

namespace FaceAttendanceSystem.Infrastructure
{
    /// <summary>Shared plumbing: a per-request DbContext, current user, audit and flash helpers.</summary>
    public abstract class BaseController : Controller
    {
        private AppDbContext _db;

        protected AppDbContext Db => _db ?? (_db = new AppDbContext());

        protected AppPrincipal CurrentUser => User as AppPrincipal;

        protected int CurrentUserId => CurrentUser?.UserId ?? 0;

        protected void Audit(string action, string entityType = null, object entityId = null, string details = null)
        {
            AuditLogger.Add(Db, HttpContext, action, entityType, entityId, details);
        }

        protected void Flash(string message, string type = "success")
        {
            TempData["FlashMessage"] = message;
            TempData["FlashType"] = type;
        }

        /// <summary>Teacher.Id of the signed-in user, or null when the user is not a teacher.</summary>
        protected int? CurrentTeacherId()
        {
            var userId = CurrentUserId;
            var id = Db.Teachers.Where(t => t.UserId == userId).Select(t => (int?)t.Id).FirstOrDefault();
            return id;
        }

        /// <summary>Student.Id of the signed-in user, or null when the user is not a student.</summary>
        protected int? CurrentStudentId()
        {
            var userId = CurrentUserId;
            return Db.Students.Where(s => s.UserId == userId).Select(s => (int?)s.Id).FirstOrDefault();
        }

        protected JsonResult JsonError(int statusCode, string message, object body = null)
        {
            Response.StatusCode = statusCode;
            Response.TrySkipIisCustomErrors = true;
            return Json(body ?? new { error = message, message });
        }

        protected ActionResult NotFoundResult() => ErrorResults.NotFound(HttpContext);

        protected ActionResult ForbiddenResult() => ErrorResults.Forbidden(HttpContext);

        protected override void Dispose(bool disposing)
        {
            if (disposing && _db != null)
            {
                _db.Dispose();
                _db = null;
            }
            base.Dispose(disposing);
        }
    }
}
