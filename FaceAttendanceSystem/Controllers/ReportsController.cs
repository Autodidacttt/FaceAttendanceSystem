using System;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using FaceAttendanceSystem.Infrastructure;
using FaceAttendanceSystem.Models;
using FaceAttendanceSystem.Services;
using FaceAttendanceSystem.ViewModels;

namespace FaceAttendanceSystem.Controllers
{
    /// <summary>
    /// Attendance reports by subject, by student and by date range, with CSV export.
    /// Teachers only see their own subjects / sessions; admins see everything.
    /// </summary>
    [RoleAuthorize("Admin", "Teacher")]
    public class ReportsController : BaseController
    {
        public ActionResult Index(ReportQuery query)
        {
            var model = Build(query ?? new ReportQuery());
            if (model == null) return ForbiddenResult();
            return View(model);
        }

        public ActionResult Export(ReportQuery query)
        {
            var model = Build(query ?? new ReportQuery());
            if (model == null) return ForbiddenResult();
            if (!model.HasResult)
            {
                Flash(model.Message ?? "Choose a report first.", "warning");
                return RedirectToAction("Index", query);
            }

            var csv = new Csv();
            string name;
            switch (model.Query.Type)
            {
                case "subject":
                    csv.Row("Report", model.Title).Row("Period", model.Subtitle).Row();
                    csv.Row("Roll number", "Name", "Sessions held", "Present", "Absent", "Attendance %");
                    csv.Rows(model.SubjectRows, r => new object[] { r.RollNumber, r.FullName, r.SessionsHeld, r.Present, r.Absent, r.Percent });
                    name = "subject";
                    break;
                case "student":
                    csv.Row("Report", model.Title).Row("Period", model.Subtitle).Row();
                    csv.Row("Subject code", "Subject", "Sessions held", "Present", "Absent", "Attendance %");
                    csv.Rows(model.StudentRows, r => new object[] { r.Code, r.Name, r.SessionsHeld, r.Present, r.Absent, r.Percent });
                    name = "student";
                    break;
                default:
                    csv.Row("Report", model.Title).Row("Period", model.Subtitle).Row();
                    csv.Row("Session id", "Date", "Subject code", "Subject", "Teacher", "Present", "Roster", "Attendance %");
                    csv.Rows(model.SessionRows, r => new object[]
                    {
                        r.Id, r.StartedUtc.ToLocal().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                        r.SubjectCode, r.SubjectName, r.TeacherName, r.PresentCount, r.RosterSize, r.Percent
                    });
                    name = "sessions";
                    break;
            }

            Audit("ReportExported", "Report", model.Query.Type, model.Title);
            Db.SaveChanges();
            return File(csv.ToBytes(), "text/csv", FileName(name));
        }

        /// <summary>CSV of one session's roster (linked from the session detail page).</summary>
        public ActionResult SessionCsv(int id)
        {
            var session = Db.ClassSessions.Include(cs => cs.Subject).Include(cs => cs.Teacher.User).FirstOrDefault(cs => cs.Id == id);
            if (session == null) return NotFoundResult();
            if (!User.IsInRole("Admin") && session.Teacher.UserId != CurrentUserId) return ForbiddenResult();

            var roster = new AttendanceService(Db).GetRosterStatus(session);
            var csv = new Csv()
                .Row("Subject", session.Subject.Code + " " + session.Subject.Name)
                .Row("Teacher", session.Teacher.User.FullName)
                .Row("Started", session.StartedUtc.ToLocal().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
                .Row()
                .Row("Roll number", "Name", "Status", "Method", "Time", "Match distance");
            csv.Rows(roster, r => new object[]
            {
                r.RollNumber, r.FullName, r.IsPresent ? "Present" : "Absent",
                r.Method.HasValue ? r.Method.ToString() : "",
                r.MarkedUtc.HasValue ? r.MarkedUtc.Value.ToLocal().ToString("HH:mm:ss", CultureInfo.InvariantCulture) : "",
                r.Distance.HasValue ? r.Distance.Value.ToString("0.000", CultureInfo.InvariantCulture) : ""
            });

            Audit("ReportExported", "ClassSession", id, "Session CSV");
            Db.SaveChanges();
            return File(csv.ToBytes(), "text/csv", FileName("session-" + id));
        }

        /// <summary>Builds the report for the query, or returns null when the user may not see it.</summary>
        private ReportViewModel Build(ReportQuery query)
        {
            var isAdmin = User.IsInRole("Admin");
            int? teacherId = null;
            if (!isAdmin)
            {
                teacherId = CurrentTeacherId();
                if (teacherId == null) return null;
            }

            query.Type = (query.Type ?? "").Trim().ToLowerInvariant();
            if (query.From.HasValue && query.To.HasValue && query.From > query.To)
            {
                var swap = query.From;
                query.From = query.To;
                query.To = swap;
            }

            var subjects = isAdmin
                ? Db.Subjects.AsQueryable()
                : Db.TeacherSubjects.Where(ts => ts.TeacherId == teacherId).Select(ts => ts.Subject);

            var model = new ReportViewModel
            {
                Query = query,
                SubjectOptions = subjects.OrderBy(s => s.Code)
                    .Select(s => new { s.Id, s.Code, s.Name }).ToList()
                    .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Code + " - " + s.Name, Selected = s.Id == query.SubjectId })
                    .ToList()
            };

            DateTime? fromUtc = null, toUtc = null;
            DateTime ignore;
            if (query.From.HasValue)
            {
                DateTime f;
                UiFormat.LocalDayToUtcRange(query.From.Value, out f, out ignore);
                fromUtc = f;
            }
            if (query.To.HasValue)
            {
                DateTime t;
                UiFormat.LocalDayToUtcRange(query.To.Value, out ignore, out t);
                toUtc = t;
            }
            model.Subtitle = Period(query);

            var reports = new ReportService(Db);
            switch (query.Type)
            {
                case "subject":
                {
                    if (!query.SubjectId.HasValue)
                    {
                        model.Message = "Choose a subject.";
                        break;
                    }
                    var subject = subjects.FirstOrDefault(s => s.Id == query.SubjectId.Value);
                    if (subject == null)
                    {
                        if (Db.Subjects.Any(s => s.Id == query.SubjectId.Value)) return null;
                        model.Message = "Subject not found.";
                        break;
                    }
                    model.Title = $"{subject.Code} {subject.Name} ({subject.Department}, semester {subject.Semester})";
                    model.SubjectRows = reports.SubjectReport(subject, fromUtc, toUtc);
                    model.HasResult = true;
                    break;
                }
                case "student":
                {
                    if (string.IsNullOrWhiteSpace(query.Roll))
                    {
                        model.Message = "Enter a roll number.";
                        break;
                    }
                    var roll = query.Roll.Trim();
                    var student = Db.Students.Include("User").FirstOrDefault(s => s.RollNumber == roll);
                    if (student == null)
                    {
                        model.Message = $"No student has roll number \"{roll}\".";
                        break;
                    }

                    var rows = reports.StudentSubjects(student, fromUtc, toUtc);
                    if (!isAdmin)
                    {
                        var mine = subjects.Select(s => s.Id).ToList();
                        rows = rows.Where(r => mine.Contains(r.SubjectId)).ToList();
                        if (rows.Count == 0) return null; // student is not in any of this teacher's classes
                    }
                    model.Title = $"{student.RollNumber} {student.User.FullName} ({student.Department}, semester {student.Semester})";
                    model.StudentRows = rows;
                    model.HasResult = true;
                    break;
                }
                case "date":
                {
                    if (query.SubjectId.HasValue && !subjects.Any(s => s.Id == query.SubjectId.Value)) return null;
                    model.Title = query.SubjectId.HasValue
                        ? "Sessions of " + subjects.Where(s => s.Id == query.SubjectId.Value).Select(s => s.Code).FirstOrDefault()
                        : (isAdmin ? "All sessions" : "My sessions");
                    model.SessionRows = reports.SessionReport(fromUtc, toUtc, query.SubjectId, teacherId);
                    model.HasResult = true;
                    break;
                }
                default:
                    query.Type = "";
                    break;
            }

            return model;
        }

        private static string Period(ReportQuery q)
        {
            if (!q.From.HasValue && !q.To.HasValue) return "All dates";
            var from = q.From.HasValue ? q.From.Value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) : "start";
            var to = q.To.HasValue ? q.To.Value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) : "today";
            return from + " to " + to;
        }

        private static string FileName(string kind)
        {
            return $"attendance-{kind}-{DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}.csv";
        }
    }
}
