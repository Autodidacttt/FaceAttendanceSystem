using System;
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
    public class StudentsController : BaseController
    {
        public ActionResult Index(string q, int page = 1)
        {
            var now = DateTime.UtcNow;
            var query = Db.Students.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(s => s.RollNumber.Contains(term) || s.User.FullName.Contains(term) ||
                                         s.User.Email.Contains(term) || s.Department.Contains(term));
            }

            var model = new StudentListViewModel
            {
                Query = q,
                Students = query
                    .OrderBy(s => s.RollNumber)
                    .Select(s => new StudentListItem
                    {
                        Id = s.Id,
                        RollNumber = s.RollNumber,
                        FullName = s.User.FullName,
                        Username = s.User.Username,
                        Email = s.User.Email,
                        Department = s.Department,
                        Semester = s.Semester,
                        IsActive = s.User.IsActive,
                        IsLockedOut = s.User.LockoutEndUtc != null && s.User.LockoutEndUtc > now,
                        FaceSamples = s.FaceEnrollments.Count()
                    })
                    .ToPaged(page)
            };
            return View(model);
        }

        [HttpGet]
        public ActionResult Create()
        {
            ViewBag.Departments = Departments();
            return View("Form", new StudentFormViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(StudentFormViewModel model)
        {
            model.Id = null;
            ValidateUnique(model, null, null);
            if (!ModelState.IsValid)
            {
                ViewBag.Departments = Departments();
                return View("Form", model);
            }

            string tempPassword;
            var roll = model.RollNumber.Trim();
            var user = UserAccountService.CreateUser(Db, roll, model.Email, model.FullName, UserRole.Student, out tempPassword);
            user.IsActive = model.IsActive;
            var student = new Student
            {
                User = user,
                RollNumber = roll,
                Department = model.Department.Trim(),
                Semester = model.Semester
            };
            Db.Students.Add(student);

            if (!TrySave(model)) return View("Form", model);

            Audit("StudentCreated", "Student", student.Id, $"{student.RollNumber} {user.FullName}");
            Db.SaveChanges();

            TempData["Credentials"] = new CredentialsViewModel
            {
                FullName = user.FullName,
                Username = user.Username,
                TemporaryPassword = tempPassword,
                BackController = "Students"
            };
            return RedirectToAction("Credentials");
        }

        [HttpGet]
        public ActionResult Edit(int id)
        {
            var student = Db.Students.Include("User").FirstOrDefault(s => s.Id == id);
            if (student == null) return NotFoundResult();

            ViewBag.Departments = Departments();
            return View("Form", new StudentFormViewModel
            {
                Id = student.Id,
                FullName = student.User.FullName,
                RollNumber = student.RollNumber,
                Email = student.User.Email,
                Department = student.Department,
                Semester = student.Semester,
                IsActive = student.User.IsActive
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Edit(int id, StudentFormViewModel model)
        {
            var student = Db.Students.Include("User").FirstOrDefault(s => s.Id == id);
            if (student == null) return NotFoundResult();

            model.Id = id;
            ValidateUnique(model, student.Id, student.UserId);
            if (!ModelState.IsValid)
            {
                ViewBag.Departments = Departments();
                return View("Form", model);
            }

            var roll = model.RollNumber.Trim();
            student.RollNumber = roll;
            student.Department = model.Department.Trim();
            student.Semester = model.Semester;
            student.User.Username = roll; // a student's username is always the roll number
            student.User.FullName = model.FullName.Trim();
            student.User.Email = model.Email.Trim();
            student.User.IsActive = model.IsActive;

            if (!TrySave(model)) return View("Form", model);

            Audit("StudentUpdated", "Student", student.Id,
                $"{student.RollNumber}; active={student.User.IsActive}; {student.Department} sem {student.Semester}");
            Db.SaveChanges();
            FaceCandidateCache.Invalidate(); // roster membership may have changed
            Flash($"Student {student.RollNumber} saved.");
            return RedirectToAction("Index");
        }

        [HttpGet]
        public ActionResult Delete(int id)
        {
            var student = Db.Students.Include("User").FirstOrDefault(s => s.Id == id);
            if (student == null) return NotFoundResult();
            ViewBag.AttendanceCount = Db.Attendances.Count(a => a.StudentId == id);
            return View(student);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var student = Db.Students.Include("User").FirstOrDefault(s => s.Id == id);
            if (student == null) return NotFoundResult();

            if (Db.Attendances.Any(a => a.StudentId == id))
            {
                Flash("This student has attendance records and cannot be deleted. Deactivate the account instead.", "warning");
                return RedirectToAction("Edit", new { id });
            }

            var label = $"{student.RollNumber} {student.User.FullName}";
            Db.FaceEnrollments.RemoveRange(Db.FaceEnrollments.Where(f => f.StudentId == id));
            var user = student.User;
            Db.Students.Remove(student);
            Db.Users.Remove(user);
            Audit("StudentDeleted", "Student", id, label);
            Db.SaveChanges();
            FaceCandidateCache.Invalidate();

            Flash($"Student {label} deleted.");
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ResetPassword(int id)
        {
            var student = Db.Students.Include("User").FirstOrDefault(s => s.Id == id);
            if (student == null) return NotFoundResult();

            var temp = UserAccountService.ResetPassword(student.User);
            Audit("PasswordReset", "User", student.UserId, $"Student {student.RollNumber}");
            Db.SaveChanges();

            TempData["Credentials"] = new CredentialsViewModel
            {
                FullName = student.User.FullName,
                Username = student.User.Username,
                TemporaryPassword = temp,
                BackController = "Students",
                IsReset = true
            };
            return RedirectToAction("Credentials");
        }

        /// <summary>Shows a temporary password exactly once (it is kept only in TempData).</summary>
        public ActionResult Credentials()
        {
            var model = TempData["Credentials"] as CredentialsViewModel;
            if (model == null) return RedirectToAction("Index");
            Response.Cache.SetNoStore();
            return View("~/Views/Shared/Credentials.cshtml", model);
        }

        private void ValidateUnique(StudentFormViewModel model, int? studentId, int? userId)
        {
            if (!string.IsNullOrWhiteSpace(model.RollNumber))
            {
                var roll = model.RollNumber.Trim();
                if (Db.Students.Any(s => s.RollNumber == roll && s.Id != (studentId ?? 0)))
                    ModelState.AddModelError(nameof(model.RollNumber), "Another student already has this roll number.");
                else if (UserAccountService.IsUsernameTaken(Db, roll, userId))
                    ModelState.AddModelError(nameof(model.RollNumber), "This roll number is already used as a username.");
            }

            if (!string.IsNullOrWhiteSpace(model.Email) && UserAccountService.IsEmailTaken(Db, model.Email, userId))
                ModelState.AddModelError(nameof(model.Email), "Another account already uses this email.");
        }

        private bool TrySave(StudentFormViewModel model)
        {
            try
            {
                Db.SaveChanges();
                return true;
            }
            catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
            {
                ModelState.AddModelError(string.Empty, "Roll number, username or email is already in use.");
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
