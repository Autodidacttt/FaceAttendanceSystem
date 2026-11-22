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
    public class TeachersController : BaseController
    {
        public ActionResult Index(string q, int page = 1)
        {
            var now = DateTime.UtcNow;
            var query = Db.Teachers.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(t => t.EmployeeNumber.Contains(term) || t.User.FullName.Contains(term) ||
                                         t.User.Username.Contains(term) || t.User.Email.Contains(term) ||
                                         t.Department.Contains(term));
            }

            var model = new TeacherListViewModel
            {
                Query = q,
                Teachers = query
                    .OrderBy(t => t.User.FullName)
                    .Select(t => new TeacherListItem
                    {
                        Id = t.Id,
                        EmployeeNumber = t.EmployeeNumber,
                        FullName = t.User.FullName,
                        Username = t.User.Username,
                        Email = t.User.Email,
                        Department = t.Department,
                        IsActive = t.User.IsActive,
                        IsLockedOut = t.User.LockoutEndUtc != null && t.User.LockoutEndUtc > now,
                        SubjectCodes = t.TeacherSubjects.Select(ts => ts.Subject.Code).ToList()
                    })
                    .ToPaged(page)
            };
            return View(model);
        }

        [HttpGet]
        public ActionResult Create()
        {
            ViewBag.Departments = Departments();
            return View("Form", new TeacherFormViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(TeacherFormViewModel model)
        {
            model.Id = null;
            ValidateUnique(model, null, null);
            if (!ModelState.IsValid)
            {
                ViewBag.Departments = Departments();
                return View("Form", model);
            }

            string tempPassword;
            var user = UserAccountService.CreateUser(Db, model.Username, model.Email, model.FullName, UserRole.Teacher, out tempPassword);
            user.IsActive = model.IsActive;
            var teacher = new Teacher
            {
                User = user,
                EmployeeNumber = model.EmployeeNumber.Trim(),
                Department = model.Department.Trim()
            };
            Db.Teachers.Add(teacher);

            if (!TrySave()) return View("Form", model);

            Audit("TeacherCreated", "Teacher", teacher.Id, $"{teacher.EmployeeNumber} {user.FullName}");
            Db.SaveChanges();

            TempData["Credentials"] = new CredentialsViewModel
            {
                FullName = user.FullName,
                Username = user.Username,
                TemporaryPassword = tempPassword,
                BackController = "Teachers"
            };
            return RedirectToAction("Credentials");
        }

        [HttpGet]
        public ActionResult Edit(int id)
        {
            var teacher = Db.Teachers.Include("User").FirstOrDefault(t => t.Id == id);
            if (teacher == null) return NotFoundResult();

            ViewBag.Departments = Departments();
            return View("Form", new TeacherFormViewModel
            {
                Id = teacher.Id,
                FullName = teacher.User.FullName,
                Username = teacher.User.Username,
                Email = teacher.User.Email,
                EmployeeNumber = teacher.EmployeeNumber,
                Department = teacher.Department,
                IsActive = teacher.User.IsActive
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Edit(int id, TeacherFormViewModel model)
        {
            var teacher = Db.Teachers.Include("User").FirstOrDefault(t => t.Id == id);
            if (teacher == null) return NotFoundResult();

            model.Id = id;
            ValidateUnique(model, teacher.Id, teacher.UserId);
            if (!ModelState.IsValid)
            {
                ViewBag.Departments = Departments();
                return View("Form", model);
            }

            teacher.EmployeeNumber = model.EmployeeNumber.Trim();
            teacher.Department = model.Department.Trim();
            teacher.User.Username = model.Username.Trim();
            teacher.User.FullName = model.FullName.Trim();
            teacher.User.Email = model.Email.Trim();
            teacher.User.IsActive = model.IsActive;

            if (!TrySave()) return View("Form", model);

            Audit("TeacherUpdated", "Teacher", teacher.Id, $"{teacher.EmployeeNumber}; active={teacher.User.IsActive}");
            Db.SaveChanges();
            Flash($"Teacher {teacher.User.FullName} saved.");
            return RedirectToAction("Index");
        }

        [HttpGet]
        public ActionResult Delete(int id)
        {
            var teacher = Db.Teachers.Include("User").FirstOrDefault(t => t.Id == id);
            if (teacher == null) return NotFoundResult();
            ViewBag.SessionCount = Db.ClassSessions.Count(cs => cs.TeacherId == id);
            return View(teacher);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var teacher = Db.Teachers.Include("User").FirstOrDefault(t => t.Id == id);
            if (teacher == null) return NotFoundResult();

            if (Db.ClassSessions.Any(cs => cs.TeacherId == id))
            {
                Flash("This teacher has class sessions on record and cannot be deleted. Deactivate the account instead.", "warning");
                return RedirectToAction("Edit", new { id });
            }

            var label = $"{teacher.EmployeeNumber} {teacher.User.FullName}";
            Db.TeacherSubjects.RemoveRange(Db.TeacherSubjects.Where(ts => ts.TeacherId == id));
            var user = teacher.User;
            Db.Teachers.Remove(teacher);
            Db.Users.Remove(user);
            Audit("TeacherDeleted", "Teacher", id, label);
            Db.SaveChanges();

            Flash($"Teacher {label} deleted.");
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ResetPassword(int id)
        {
            var teacher = Db.Teachers.Include("User").FirstOrDefault(t => t.Id == id);
            if (teacher == null) return NotFoundResult();

            var temp = UserAccountService.ResetPassword(teacher.User);
            Audit("PasswordReset", "User", teacher.UserId, $"Teacher {teacher.EmployeeNumber}");
            Db.SaveChanges();

            TempData["Credentials"] = new CredentialsViewModel
            {
                FullName = teacher.User.FullName,
                Username = teacher.User.Username,
                TemporaryPassword = temp,
                BackController = "Teachers",
                IsReset = true
            };
            return RedirectToAction("Credentials");
        }

        public ActionResult Credentials()
        {
            var model = TempData["Credentials"] as CredentialsViewModel;
            if (model == null) return RedirectToAction("Index");
            Response.Cache.SetNoStore();
            return View("~/Views/Shared/Credentials.cshtml", model);
        }

        private void ValidateUnique(TeacherFormViewModel model, int? teacherId, int? userId)
        {
            if (!string.IsNullOrWhiteSpace(model.EmployeeNumber))
            {
                var number = model.EmployeeNumber.Trim();
                if (Db.Teachers.Any(t => t.EmployeeNumber == number && t.Id != (teacherId ?? 0)))
                    ModelState.AddModelError(nameof(model.EmployeeNumber), "Another teacher already has this employee number.");
            }

            if (!string.IsNullOrWhiteSpace(model.Username) && UserAccountService.IsUsernameTaken(Db, model.Username, userId))
                ModelState.AddModelError(nameof(model.Username), "This username is already taken.");

            if (!string.IsNullOrWhiteSpace(model.Email) && UserAccountService.IsEmailTaken(Db, model.Email, userId))
                ModelState.AddModelError(nameof(model.Email), "Another account already uses this email.");
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
                ModelState.AddModelError(string.Empty, "Employee number, username or email is already in use.");
                ViewBag.Departments = Departments();
                return false;
            }
        }

        private string[] Departments()
        {
            return Db.Subjects.Select(s => s.Department)
                .Union(Db.Teachers.Select(t => t.Department))
                .Distinct().OrderBy(d => d).ToArray();
        }
    }
}
