using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Web.Mvc;
using FaceAttendanceSystem.Infrastructure;
using FaceAttendanceSystem.Models;
using FaceAttendanceSystem.Services;
using FaceAttendanceSystem.ViewModels;

namespace FaceAttendanceSystem.Controllers
{
    [RoleAuthorize("Admin")]
    public class SubjectsController : BaseController
    {
        public ActionResult Index()
        {
            var rosterSizes = new ReportService(Db).RosterSizes();
            var sessionCounts = Db.ClassSessions
                .GroupBy(cs => cs.SubjectId)
                .Select(g => new { SubjectId = g.Key, Count = g.Count() })
                .ToDictionary(x => x.SubjectId, x => x.Count);

            var subjects = Db.Subjects
                .OrderBy(s => s.Department).ThenBy(s => s.Semester).ThenBy(s => s.Code)
                .Select(s => new SubjectListItem
                {
                    Id = s.Id,
                    Code = s.Code,
                    Name = s.Name,
                    Department = s.Department,
                    Semester = s.Semester,
                    Teachers = s.TeacherSubjects.Select(ts => ts.Teacher.User.FullName).ToList()
                })
                .ToList();

            foreach (var s in subjects)
            {
                int size, sessions;
                rosterSizes.TryGetValue(ReportService.GroupKey(s.Department, s.Semester), out size);
                sessionCounts.TryGetValue(s.Id, out sessions);
                s.RosterSize = size;
                s.SessionCount = sessions;
            }

            return View(subjects);
        }

        [HttpGet]
        public ActionResult Create()
        {
            ViewBag.Departments = Departments();
            return View("Form", new SubjectFormViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(SubjectFormViewModel model)
        {
            model.Id = null;
            ValidateUnique(model, null);
            if (!ModelState.IsValid)
            {
                ViewBag.Departments = Departments();
                return View("Form", model);
            }

            var subject = new Subject
            {
                Code = model.Code.Trim().ToUpperInvariant(),
                Name = model.Name.Trim(),
                Department = model.Department.Trim(),
                Semester = model.Semester
            };
            Db.Subjects.Add(subject);
            if (!TrySave()) return View("Form", model);

            Audit("SubjectCreated", "Subject", subject.Id, $"{subject.Code} {subject.Name}");
            Db.SaveChanges();
            Flash($"Subject {subject.Code} created.");
            return RedirectToAction("Index");
        }

        [HttpGet]
        public ActionResult Edit(int id)
        {
            var subject = Db.Subjects.Find(id);
            if (subject == null) return NotFoundResult();

            ViewBag.Departments = Departments();
            return View("Form", new SubjectFormViewModel
            {
                Id = subject.Id,
                Code = subject.Code,
                Name = subject.Name,
                Department = subject.Department,
                Semester = subject.Semester
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Edit(int id, SubjectFormViewModel model)
        {
            var subject = Db.Subjects.Find(id);
            if (subject == null) return NotFoundResult();

            model.Id = id;
            ValidateUnique(model, id);
            if (!ModelState.IsValid)
            {
                ViewBag.Departments = Departments();
                return View("Form", model);
            }

            subject.Code = model.Code.Trim().ToUpperInvariant();
            subject.Name = model.Name.Trim();
            subject.Department = model.Department.Trim();
            subject.Semester = model.Semester;
            if (!TrySave()) return View("Form", model);

            Audit("SubjectUpdated", "Subject", subject.Id, $"{subject.Code}; {subject.Department} sem {subject.Semester}");
            Db.SaveChanges();
            FaceCandidateCache.Invalidate(); // roster may have changed
            Flash($"Subject {subject.Code} saved.");
            return RedirectToAction("Index");
        }

        [HttpGet]
        public ActionResult Delete(int id)
        {
            var subject = Db.Subjects.Find(id);
            if (subject == null) return NotFoundResult();
            ViewBag.SessionCount = Db.ClassSessions.Count(cs => cs.SubjectId == id);
            return View(subject);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var subject = Db.Subjects.Find(id);
            if (subject == null) return NotFoundResult();

            if (Db.ClassSessions.Any(cs => cs.SubjectId == id))
            {
                Flash("This subject has class sessions on record and cannot be deleted.", "warning");
                return RedirectToAction("Index");
            }

            var label = $"{subject.Code} {subject.Name}";
            Db.TeacherSubjects.RemoveRange(Db.TeacherSubjects.Where(ts => ts.SubjectId == id));
            Db.Subjects.Remove(subject);
            Audit("SubjectDeleted", "Subject", id, label);
            Db.SaveChanges();
            Flash($"Subject {label} deleted.");
            return RedirectToAction("Index");
        }

        private void ValidateUnique(SubjectFormViewModel model, int? subjectId)
        {
            if (string.IsNullOrWhiteSpace(model.Code)) return;
            var code = model.Code.Trim().ToUpperInvariant();
            if (Db.Subjects.Any(s => s.Code == code && s.Id != (subjectId ?? 0)))
                ModelState.AddModelError(nameof(model.Code), "Another subject already has this code.");
        }

        private bool TrySave()
        {
            try
            {
                Db.SaveChanges();
                return true;
            }
            catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
            {
                ModelState.AddModelError(nameof(SubjectFormViewModel.Code), "Another subject already has this code.");
                ViewBag.Departments = Departments();
                return false;
            }
        }

        private string[] Departments()
        {
            return Db.Subjects.Select(s => s.Department)
                .Union(Db.Students.Select(s => s.Department))
                .Distinct().OrderBy(d => d).ToArray();
        }
    }
}
