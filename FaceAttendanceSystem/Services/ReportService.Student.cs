using System;
using System.Collections.Generic;
using System.Linq;
using FaceAttendanceSystem.Models;
using FaceAttendanceSystem.ViewModels;

namespace FaceAttendanceSystem.Services
{
    public partial class ReportService
    {
        /// <summary>Per-subject attendance for one student (subjects of the student's department/semester).</summary>
        public List<SubjectAttendanceRow> StudentSubjects(Student student, DateTime? fromUtc = null, DateTime? toUtc = null)
        {
            var subjects = _db.Subjects
                .Where(s => s.Department == student.Department && s.Semester == student.Semester)
                .OrderBy(s => s.Code)
                .Select(s => new { s.Id, s.Code, s.Name })
                .ToList();

            var sessions = SessionsInRange(fromUtc, toUtc)
                .Where(cs => cs.Subject.Department == student.Department && cs.Subject.Semester == student.Semester);

            var held = sessions
                .GroupBy(cs => cs.SubjectId)
                .Select(g => new { SubjectId = g.Key, Count = g.Count() })
                .ToDictionary(x => x.SubjectId, x => x.Count);

            var studentId = student.Id;
            var present = sessions
                .SelectMany(cs => cs.Attendances)
                .Where(a => a.StudentId == studentId && a.Status == AttendanceStatus.Present)
                .GroupBy(a => a.ClassSession.SubjectId)
                .Select(g => new { SubjectId = g.Key, Count = g.Count() })
                .ToDictionary(x => x.SubjectId, x => x.Count);

            return subjects.Select(s =>
            {
                int h, p;
                held.TryGetValue(s.Id, out h);
                present.TryGetValue(s.Id, out p);
                p = Math.Min(p, h);
                return new SubjectAttendanceRow
                {
                    SubjectId = s.Id,
                    Code = s.Code,
                    Name = s.Name,
                    SessionsHeld = h,
                    Present = p,
                    Percent = Percent(p, h)
                };
            }).ToList();
        }

        /// <summary>Most recent sessions the student was expected at, with their status.</summary>
        public List<StudentSessionEntry> StudentRecentSessions(Student student, int take)
        {
            var studentId = student.Id;
            return _db.ClassSessions
                .Where(cs => cs.Subject.Department == student.Department && cs.Subject.Semester == student.Semester)
                .OrderByDescending(cs => cs.StartedUtc)
                .Take(take)
                .Select(cs => new
                {
                    cs.Id,
                    cs.Subject.Code,
                    cs.Subject.Name,
                    cs.StartedUtc,
                    Record = cs.Attendances.Where(a => a.StudentId == studentId).Select(a => new { a.Status, a.Method, a.MarkedUtc }).FirstOrDefault()
                })
                .ToList()
                .Select(x => new StudentSessionEntry
                {
                    SessionId = x.Id,
                    SubjectCode = x.Code,
                    SubjectName = x.Name,
                    StartedUtc = x.StartedUtc,
                    Present = x.Record != null && x.Record.Status == AttendanceStatus.Present,
                    Method = x.Record?.Method,
                    MarkedUtc = x.Record?.MarkedUtc
                })
                .ToList();
        }

        private IQueryable<ClassSession> SessionsInRange(DateTime? fromUtc, DateTime? toUtc)
        {
            var sessions = _db.ClassSessions.AsQueryable();
            if (fromUtc.HasValue)
            {
                var f = fromUtc.Value;
                sessions = sessions.Where(cs => cs.StartedUtc >= f);
            }
            if (toUtc.HasValue)
            {
                var t = toUtc.Value;
                sessions = sessions.Where(cs => cs.StartedUtc < t);
            }
            return sessions;
        }
    }
}
