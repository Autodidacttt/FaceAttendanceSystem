using System;
using System.Linq;
using System.Web.Mvc;
using FaceAttendanceSystem.Infrastructure;
using FaceAttendanceSystem.Models;
using FaceAttendanceSystem.Services;
using FaceAttendanceSystem.ViewModels;

namespace FaceAttendanceSystem.Controllers
{
    [RoleAuthorize("Admin")]
    public class AdminController : BaseController
    {
        public ActionResult Index() => RedirectToAction("Dashboard");

        public ActionResult Dashboard()
        {
            DateTime startUtc, endUtc;
            UiFormat.LocalDayToUtcRange(DateTime.Now, out startUtc, out endUtc);

            var reports = new ReportService(Db);
            int sessionsToday, present, expected;
            reports.SessionTotals(startUtc, endUtc, out sessionsToday, out present, out expected);

            var model = new AdminDashboardViewModel
            {
                StudentCount = Db.Students.Count(s => s.User.IsActive),
                TeacherCount = Db.Teachers.Count(t => t.User.IsActive),
                SubjectCount = Db.Subjects.Count(),
                SessionsToday = sessionsToday,
                ActiveSessions = Db.ClassSessions.Count(cs => cs.EndedUtc == null),
                TodayPresent = present,
                TodayExpected = expected,
                TodayPercent = ReportService.Percent(present, expected),
                StudentsWithoutFace = Db.Students.Count(s => s.User.IsActive && !s.FaceEnrollments.Any()),
                LowestAttendance = reports.StudentTotals()
                    .Where(r => r.SessionsHeld > 0)
                    .OrderBy(r => r.Percent)
                    .ThenBy(r => r.RollNumber)
                    .Take(5)
                    .ToList(),
                RecentActivity = Db.AuditLogs.OrderByDescending(a => a.Id).Take(8).ToList()
            };

            return View(model);
        }
    }
}
