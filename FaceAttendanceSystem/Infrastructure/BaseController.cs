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
