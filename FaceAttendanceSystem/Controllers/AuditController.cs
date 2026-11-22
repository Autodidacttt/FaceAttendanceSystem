using System.Linq;
using System.Web.Mvc;
using FaceAttendanceSystem.Infrastructure;
using FaceAttendanceSystem.ViewModels;

namespace FaceAttendanceSystem.Controllers
{
    [RoleAuthorize("Admin")]
    public class AuditController : BaseController
    {
        public ActionResult Index(string q, int page = 1)
        {
            var query = Db.AuditLogs.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(a => a.Action.Contains(term) || a.Username.Contains(term) ||
                                         a.EntityType.Contains(term) || a.Details.Contains(term));
            }

            return View(new AuditListViewModel
            {
                Query = q,
                Entries = query.OrderByDescending(a => a.Id).ToPaged(page, 50)
            });
        }
    }
}
