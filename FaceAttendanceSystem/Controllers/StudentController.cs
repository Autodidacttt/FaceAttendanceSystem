using System.Linq;
using System.Web.Mvc;
using FaceAttendanceSystem.Infrastructure;
using FaceAttendanceSystem.Services;
using FaceAttendanceSystem.ViewModels;

namespace FaceAttendanceSystem.Controllers
{
    /// <summary>Student portal: read-only view of the signed-in student's own attendance.</summary>
    [RoleAuthorize("Student")]
    public class StudentController : BaseController
    {
        public ActionResult Index() => RedirectToAction("Dashboard");

        public ActionResult Dashboard()
        {
            var userId = CurrentUserId;
            var student = Db.Students.Include("User").FirstOrDefault(s => s.UserId == userId);
            if (student == null) return ForbiddenResult();

            var reports = new ReportService(Db);
            var subjects = reports.StudentSubjects(student);
            var held = subjects.Sum(s => s.SessionsHeld);
            var present = subjects.Sum(s => s.Present);

            return View(new StudentDashboardViewModel
            {
                FullName = student.User.FullName,
                RollNumber = student.RollNumber,
                Department = student.Department,
                Semester = student.Semester,
                SessionsHeld = held,
                Present = present,
                Percent = ReportService.Percent(present, held),
                FaceEnrolled = Db.FaceEnrollments.Any(f => f.StudentId == student.Id),
                Subjects = subjects,
                Recent = reports.StudentRecentSessions(student, 10)
            });
        }

        // Controller already has a "Profile" property, so the method gets another name.
        [ActionName("Profile")]
        public ActionResult ProfilePage()
        {
            var userId = CurrentUserId;
            var model = Db.Students
                .Where(s => s.UserId == userId)
                .Select(s => new StudentProfileViewModel
                {
                    FullName = s.User.FullName,
                    Username = s.User.Username,
                    Email = s.User.Email,
                    RollNumber = s.RollNumber,
                    Department = s.Department,
                    Semester = s.Semester,
                    FaceSamples = s.FaceEnrollments.Count(),
                    EnrolledUtc = s.FaceEnrollments.Max(f => (System.DateTime?)f.CreatedUtc),
                    LastLoginUtc = s.User.LastLoginUtc
                })
                .FirstOrDefault();
            if (model == null) return ForbiddenResult();

            model.FaceEnrolled = model.FaceSamples > 0;
            return View(model);
        }
    }
}
