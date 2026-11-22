using System.Linq;
using System.Web.Mvc;
using FaceAttendanceSystem.Infrastructure;
using FaceAttendanceSystem.Models;
using FaceAttendanceSystem.ViewModels;

namespace FaceAttendanceSystem.Controllers
{
    /// <summary>Assigns subjects to teachers (a teacher can only run sessions for assigned subjects).</summary>
    [RoleAuthorize("Admin")]
    public class AssignmentsController : BaseController
    {
        public ActionResult Index()
        {
            return View(Build(new AssignmentsViewModel()));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Add(AssignmentsViewModel model)
        {
            if (!ModelState.IsValid) return View("Index", Build(model));

            var teacher = Db.Teachers.Include("User").FirstOrDefault(t => t.Id == model.TeacherId);
            var subject = Db.Subjects.Find(model.SubjectId);
            if (teacher == null || subject == null)
            {
                ModelState.AddModelError(string.Empty, "Choose a valid teacher and subject.");
                return View("Index", Build(model));
            }

            if (Db.TeacherSubjects.Any(ts => ts.TeacherId == teacher.Id && ts.SubjectId == subject.Id))
            {
                Flash($"{teacher.User.FullName} already teaches {subject.Code}.", "info");
                return RedirectToAction("Index");
            }

            var assignment = new TeacherSubject { TeacherId = teacher.Id, SubjectId = subject.Id };
            Db.TeacherSubjects.Add(assignment);
            Db.SaveChanges();
            Audit("SubjectAssigned", "TeacherSubject", assignment.Id, $"{subject.Code} -> {teacher.EmployeeNumber}");
            Db.SaveChanges();

            Flash($"{subject.Code} assigned to {teacher.User.FullName}.");
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Remove(int id)
        {
            var assignment = Db.TeacherSubjects.Include("Teacher.User").Include("Subject").FirstOrDefault(ts => ts.Id == id);
            if (assignment == null) return NotFoundResult();

            var label = $"{assignment.Subject.Code} -> {assignment.Teacher.EmployeeNumber}";
            Db.TeacherSubjects.Remove(assignment);
            Audit("SubjectUnassigned", "TeacherSubject", id, label);
            Db.SaveChanges();

            Flash($"Removed {assignment.Subject.Code} from {assignment.Teacher.User.FullName}.");
            return RedirectToAction("Index");
        }

        private AssignmentsViewModel Build(AssignmentsViewModel model)
        {
            model.Assignments = Db.TeacherSubjects
                .OrderBy(ts => ts.Teacher.User.FullName).ThenBy(ts => ts.Subject.Code)
                .Select(ts => new AssignmentRow
                {
                    Id = ts.Id,
                    TeacherName = ts.Teacher.User.FullName,
                    EmployeeNumber = ts.Teacher.EmployeeNumber,
                    SubjectCode = ts.Subject.Code,
                    SubjectName = ts.Subject.Name
                })
                .ToList();

            model.TeacherOptions = Db.Teachers
                .Where(t => t.User.IsActive)
                .OrderBy(t => t.User.FullName)
                .Select(t => new { t.Id, t.User.FullName, t.EmployeeNumber })
                .ToList()
                .Select(t => new SelectListItem { Value = t.Id.ToString(), Text = $"{t.FullName} ({t.EmployeeNumber})" });

            model.SubjectOptions = Db.Subjects
                .OrderBy(s => s.Code)
                .Select(s => new { s.Id, s.Code, s.Name })
                .ToList()
                .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = $"{s.Code} - {s.Name}" });

            return model;
        }
    }
}
