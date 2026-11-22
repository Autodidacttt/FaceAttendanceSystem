using System;
using System.Collections.Generic;
using System.Linq;
using FaceAttendanceSystem.Models;
using FaceAttendanceSystem.ViewModels;

namespace FaceAttendanceSystem.Services
{
    public partial class ReportService
    {
        /// <summary>Report by subject: each active roster student's attendance in the subject's sessions.</summary>
        public List<StudentAttendanceRow> SubjectReport(Subject subject, DateTime? fromUtc, DateTime? toUtc)
        {
            var subjectId = subject.Id;
            var sessionIds = SessionsInRange(fromUtc, toUtc)
                .Where(cs => cs.SubjectId == subjectId)
                .Select(cs => cs.Id)
                .ToList();
            var held = sessionIds.Count;

            var present = sessionIds.Count == 0
                ? new Dictionary<int, int>()
                : _db.Attendances
                    .Where(a => sessionIds.Contains(a.ClassSessionId) && a.Status == AttendanceStatus.Present)
                    .GroupBy(a => a.StudentId)
                    .Select(g => new { StudentId = g.Key, Count = g.Count() })
                    .ToDictionary(x => x.StudentId, x => x.Count);

            return RosterService.RosterFor(_db, subject)
                .OrderBy(s => s.RollNumber)
                .Select(s => new { s.Id, s.RollNumber, s.User.FullName, s.Department, s.Semester })
                .ToList()
                .Select(s =>
                {
                    int p;
                    present.TryGetValue(s.Id, out p);
                    p = Math.Min(p, held);
                    return new StudentAttendanceRow
                    {
                        StudentId = s.Id,
                        RollNumber = s.RollNumber,
                        FullName = s.FullName,
                        Department = s.Department,
                        Semester = s.Semester,
                        SessionsHeld = held,
                        Present = p,
                        Percent = Percent(p, held)
                    };
                })
                .ToList();
        }

        /// <summary>Report by date range: every session in the range with its present count and roster size.</summary>
        public List<SessionRow> SessionReport(DateTime? fromUtc, DateTime? toUtc, int? subjectId, int? teacherId)
        {
            var sessions = SessionsInRange(fromUtc, toUtc);
            if (subjectId.HasValue)
            {
                var sid = subjectId.Value;
                sessions = sessions.Where(cs => cs.SubjectId == sid);
            }
            if (teacherId.HasValue)
            {
                var tid = teacherId.Value;
                sessions = sessions.Where(cs => cs.TeacherId == tid);
            }

            var rows = sessions
                .OrderBy(cs => cs.StartedUtc)
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

            var sizes = RosterSizes();
            foreach (var row in rows)
            {
                int size;
                sizes.TryGetValue(GroupKey(row.Department, row.Semester), out size);
                row.RosterSize = Math.Max(size, row.PresentCount);
            }
            return rows;
        }
    }
}
