using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using FaceAttendanceSystem.Infrastructure;
using FaceAttendanceSystem.Models;
using FaceAttendanceSystem.Services;
using FaceAttendanceSystem.ViewModels;

namespace FaceAttendanceSystem.Controllers
{
    /// <summary>
    /// Class sessions. Teachers start/run/end sessions for their own subjects;
    /// admins can view every session and correct attendance.
    /// </summary>
    [RoleAuthorize("Teacher", "Admin")]
    public class SessionController : BaseController
    {
        [HttpGet, RoleAuthorize("Teacher")]
        public ActionResult Start(int? subjectId)
        {
            var teacherId = CurrentTeacherId();
            if (teacherId == null) return ForbiddenResult();

            return View(new StartSessionViewModel
            {
                Subjects = SessionQueries.SubjectCards(Db, teacherId.Value),
                SubjectId = subjectId
            });
        }

        [HttpPost, ValidateAntiForgeryToken, RoleAuthorize("Teacher")]
        public ActionResult Start(StartSessionViewModel model)
        {
            var teacherId = CurrentTeacherId();
            if (teacherId == null) return ForbiddenResult();

            if (!ModelState.IsValid)
            {
                model.Subjects = SessionQueries.SubjectCards(Db, teacherId.Value);
                return View(model);
            }

            var assigned = Db.TeacherSubjects.Any(ts => ts.TeacherId == teacherId && ts.SubjectId == model.SubjectId);
            if (!assigned) return ForbiddenResult();

            var active = Db.ClassSessions
                .Where(cs => cs.TeacherId == teacherId && cs.SubjectId == model.SubjectId && cs.EndedUtc == null)
                .Select(cs => (int?)cs.Id)
                .FirstOrDefault();
            if (active.HasValue)
            {
                Flash("This subject already has a session in progress, so it was reopened.", "info");
                return RedirectToAction("Live", new { id = active.Value });
            }

            var session = new ClassSession
            {
                SubjectId = model.SubjectId.Value,
                TeacherId = teacherId.Value,
                StartedUtc = DateTime.UtcNow,
                Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim()
            };
            Db.ClassSessions.Add(session);
            Db.SaveChanges();
            Audit("SessionStarted", "ClassSession", session.Id, $"Subject {session.SubjectId}");
            Db.SaveChanges();

            return RedirectToAction("Live", new { id = session.Id });
        }

        [HttpGet, RoleAuthorize("Teacher")]
        public ActionResult Live(int id)
        {
            var session = LoadSession(id);
            if (session == null) return NotFoundResult();
            if (!IsOwner(session)) return ForbiddenResult();
            if (!session.IsActive) return RedirectToAction("Detail", new { id });

            return View(BuildPage(session, canEdit: true));
        }

        [HttpPost, ValidateAntiForgeryToken, RoleAuthorize("Teacher")]
        public ActionResult End(int id)
        {
            var session = LoadSession(id);
            if (session == null) return NotFoundResult();
            if (!IsOwner(session)) return ForbiddenResult();

            if (session.IsActive)
            {
                session.EndedUtc = DateTime.UtcNow;
                var present = Db.Attendances.Count(a => a.ClassSessionId == id && a.Status == AttendanceStatus.Present);
                Audit("SessionEnded", "ClassSession", id, $"{session.Subject.Code}: {present} present");
                Db.SaveChanges();
                Flash("Session ended. Review the attendance below.");
            }
            return RedirectToAction("Detail", new { id });
        }

        [HttpGet]
        public ActionResult Detail(int id)
        {
            var session = LoadSession(id);
            if (session == null) return NotFoundResult();
            if (!CanView(session)) return ForbiddenResult();

            return View(BuildPage(session, canEdit: IsOwner(session) || User.IsInRole("Admin")));
        }

        /// <summary>Manual present/absent correction from the session detail page (form post).</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Override(int id, int studentId, bool present)
        {
            var session = LoadSession(id);
            if (session == null) return NotFoundResult();
            if (!(IsOwner(session) || User.IsInRole("Admin"))) return ForbiddenResult();

            var student = Db.Students.Include("User").FirstOrDefault(s => s.Id == studentId);
            if (student == null) return NotFoundResult();

            new AttendanceService(Db).ManualOverride(session, studentId, present, CurrentUserId);
            Audit("AttendanceOverride", "ClassSession", id,
                $"{student.RollNumber} marked {(present ? "present" : "absent")} manually");
            Db.SaveChanges();

            Flash($"{student.User.FullName} marked {(present ? "present" : "absent")}.");
            return RedirectToAction("Detail", new { id });
        }

        [HttpGet]
        public ActionResult History(int? subjectId, DateTime? from, DateTime? to, string roll, int page = 1)
        {
            var teacherId = User.IsInRole("Teacher") ? CurrentTeacherId() : null;
            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin && teacherId == null) return ForbiddenResult();

            var model = new HistoryViewModel { SubjectId = subjectId, From = from, To = to, Roll = roll };

            var subjects = isAdmin
                ? Db.Subjects.AsQueryable()
                : Db.TeacherSubjects.Where(ts => ts.TeacherId == teacherId).Select(ts => ts.Subject);
            model.SubjectOptions = subjects.OrderBy(s => s.Code)
                .Select(s => new { s.Id, s.Code, s.Name }).ToList()
                .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Code + " - " + s.Name, Selected = s.Id == subjectId });

            var sessions = Db.ClassSessions.AsQueryable();
            if (!isAdmin) sessions = sessions.Where(cs => cs.TeacherId == teacherId);
            if (subjectId.HasValue) sessions = sessions.Where(cs => cs.SubjectId == subjectId.Value);
            if (from.HasValue)
            {
                DateTime fromUtc, ignore;
                UiFormat.LocalDayToUtcRange(from.Value, out fromUtc, out ignore);
                sessions = sessions.Where(cs => cs.StartedUtc >= fromUtc);
            }
            if (to.HasValue)
            {
                DateTime ignore, toUtc;
                UiFormat.LocalDayToUtcRange(to.Value, out ignore, out toUtc);
                sessions = sessions.Where(cs => cs.StartedUtc < toUtc);
            }

            Student student = null;
            if (!string.IsNullOrWhiteSpace(roll))
            {
                var r = roll.Trim();
                student = Db.Students.Include("User").FirstOrDefault(s => s.RollNumber == r);
                if (student == null)
                {
                    model.Message = $"No student has roll number \"{r}\".";
                    sessions = sessions.Where(cs => false);
                }
                else
                {
                    model.RollStudentName = student.User.FullName;
                    var dept = student.Department;
                    var sem = student.Semester;
                    var sid = student.Id;
                    // Sessions this student was expected at, or attended.
                    sessions = sessions.Where(cs => (cs.Subject.Department == dept && cs.Subject.Semester == sem) ||
                                                    cs.Attendances.Any(a => a.StudentId == sid));
                }
            }

            var paged = sessions
                .OrderByDescending(cs => cs.StartedUtc)
                .Select(cs => new SessionRow
                {
                    Id = cs.Id,
                    SubjectCode = cs.Subject.Code,
                    SubjectName = cs.Subject.Name,
                    Department = cs.Subject.Department,
                    Semester = cs.Subject.Semester,
                    TeacherName = cs.Teacher.User.FullName,
                    StartedUtc = cs.StartedUtc,
                    EndedUtc = cs.EndedUtc,
                    PresentCount = cs.Attendances.Count(a => a.Status == AttendanceStatus.Present)
                })
                .ToPaged(page);
            SessionQueries.FillRosterSizes(Db, paged.Items);

            if (student != null)
            {
                var ids = paged.Items.Select(s => s.Id).ToList();
                var presentIn = Db.Attendances
                    .Where(a => a.StudentId == student.Id && ids.Contains(a.ClassSessionId) && a.Status == AttendanceStatus.Present)
                    .Select(a => a.ClassSessionId)
                    .ToList();
                foreach (var row in paged.Items) row.StudentStatus = presentIn.Contains(row.Id) ? "Present" : "Absent";
            }

            model.Sessions = paged;
            return View(model);
        }

        /// <summary>Streams a stored attendance snapshot (App_Data is never served directly).</summary>
        [HttpGet]
        public ActionResult Snapshot(int id)
        {
            var record = Db.Attendances.Include("ClassSession").FirstOrDefault(a => a.Id == id);
            if (record == null || string.IsNullOrEmpty(record.SnapshotPath)) return NotFoundResult();
            if (!CanView(record.ClassSession)) return ForbiddenResult();

            var appData = Server.MapPath("~/App_Data");
            var full = Path.GetFullPath(Path.Combine(appData, record.SnapshotPath));
            if (!full.StartsWith(Path.GetFullPath(appData), StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(full))
                return NotFoundResult();

            Response.Cache.SetCacheability(System.Web.HttpCacheability.Private);
            return File(full, "image/jpeg");
        }

        private ClassSession LoadSession(int id)
        {
            return Db.ClassSessions
                .Include(cs => cs.Subject)
                .Include(cs => cs.Teacher.User)
                .FirstOrDefault(cs => cs.Id == id);
        }

        private bool IsOwner(ClassSession session)
        {
            return User.IsInRole("Teacher") && session.Teacher.UserId == CurrentUserId;
        }

        private bool CanView(ClassSession session)
        {
            if (User.IsInRole("Admin")) return true;
            var userId = CurrentUserId;
            return User.IsInRole("Teacher") && Db.ClassSessions.Any(cs => cs.Id == session.Id && cs.Teacher.UserId == userId);
        }

        private SessionPageViewModel BuildPage(ClassSession session, bool canEdit)
        {
            return new SessionPageViewModel
            {
                SessionId = session.Id,
                SubjectCode = session.Subject.Code,
                SubjectName = session.Subject.Name,
                Department = session.Subject.Department,
                Semester = session.Subject.Semester,
                TeacherName = session.Teacher.User.FullName,
                Notes = session.Notes,
                StartedUtc = session.StartedUtc,
                EndedUtc = session.EndedUtc,
                CanEdit = canEdit,
                MatchThreshold = SettingsService.GetMatchThreshold(Db),
                Roster = new AttendanceService(Db).GetRosterStatus(session)
            };
        }
    }
}
