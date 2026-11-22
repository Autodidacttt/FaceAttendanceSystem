using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using FaceAttendanceSystem.Data;
using FaceAttendanceSystem.Infrastructure;
using FaceAttendanceSystem.Models;
using FaceAttendanceSystem.Services;
using FaceAttendanceSystem.ViewModels;

namespace FaceAttendanceSystem.Controllers
{
    [RoleAuthorize("Teacher")]
    public class TeacherController : BaseController
    {
        public ActionResult Index() => RedirectToAction("Dashboard");

        public ActionResult Dashboard()
        {
            var teacherId = CurrentTeacherId();
            if (teacherId == null) return ForbiddenResult();

            DateTime startUtc, endUtc;
            UiFormat.LocalDayToUtcRange(DateTime.Now, out startUtc, out endUtc);

            var sessions = SessionQueries.Rows(Db, Db.ClassSessions.Where(cs => cs.TeacherId == teacherId &&
                (cs.EndedUtc == null || (cs.StartedUtc >= startUtc && cs.StartedUtc < endUtc))));

            var model = new TeacherDashboardViewModel
            {
                TeacherName = CurrentUser.FullName,
                Subjects = SessionQueries.SubjectCards(Db, teacherId.Value),
                ActiveSessions = sessions.Where(s => s.IsActive).ToList(),
                TodaySessions = sessions.Where(s => s.StartedUtc >= startUtc && s.StartedUtc < endUtc).ToList()
            };
            return View(model);
        }
    }

    /// <summary>Queries shared by the teacher dashboard and the session pages.</summary>
    public static class SessionQueries
    {
        /// <summary>Subjects assigned to a teacher, with roster, enrollment and session counts.</summary>
        public static List<TeacherSubjectCard> SubjectCards(AppDbContext db, int teacherId)
        {
            var cards = db.TeacherSubjects
                .Where(ts => ts.TeacherId == teacherId)
                .OrderBy(ts => ts.Subject.Code)
                .Select(ts => new TeacherSubjectCard
                {
                    SubjectId = ts.SubjectId,
                    Code = ts.Subject.Code,
                    Name = ts.Subject.Name,
                    Department = ts.Subject.Department,
                    Semester = ts.Subject.Semester
                })
                .ToList();

            foreach (var card in cards)
            {
                var roster = RosterService.RosterFor(db, card.Department, card.Semester);
                card.RosterSize = roster.Count();
                card.EnrolledCount = roster.Count(s => s.FaceEnrollments.Any());
                var subjectId = card.SubjectId;
                card.SessionsHeld = db.ClassSessions.Count(cs => cs.SubjectId == subjectId);
                card.ActiveSessionId = db.ClassSessions
                    .Where(cs => cs.SubjectId == subjectId && cs.TeacherId == teacherId && cs.EndedUtc == null)
                    .OrderByDescending(cs => cs.StartedUtc)
                    .Select(cs => (int?)cs.Id)
                    .FirstOrDefault();
            }
            return cards;
        }

        /// <summary>Projects sessions to list rows with present counts and current roster sizes (newest first).</summary>
        public static List<SessionRow> Rows(AppDbContext db, IQueryable<ClassSession> sessions)
        {
            var rows = sessions
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
                .ToList();
            FillRosterSizes(db, rows);
            return rows;
        }

        public static void FillRosterSizes(AppDbContext db, IEnumerable<SessionRow> rows)
        {
            var sizes = new ReportService(db).RosterSizes();
            foreach (var row in rows)
            {
                int size;
                sizes.TryGetValue(ReportService.GroupKey(row.Department, row.Semester), out size);
                row.RosterSize = Math.Max(size, row.PresentCount);
            }
        }
    }
}
