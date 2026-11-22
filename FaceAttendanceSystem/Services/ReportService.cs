using System;
using System.Collections.Generic;
using System.Linq;
using FaceAttendanceSystem.Data;
using FaceAttendanceSystem.Models;
using FaceAttendanceSystem.ViewModels;

namespace FaceAttendanceSystem.Services
{
    /// <summary>
    /// Attendance statistics. "Held" = sessions of subjects in the student's department/semester;
    /// "present" = Present rows for that student. Percent = present / held.
    /// </summary>
    public partial class ReportService
    {
        private readonly AppDbContext _db;

        public ReportService(AppDbContext db)
        {
            _db = db;
        }

        public static double Percent(int part, int whole)
        {
            return whole == 0 ? 0 : Math.Round(100.0 * part / whole, 1);
        }

        /// <summary>Overall attendance for every active student (optionally one department/semester).</summary>
        public List<StudentAttendanceRow> StudentTotals(string department = null, int? semester = null)
        {
            var students = _db.Students.Where(s => s.User.IsActive);
            if (!string.IsNullOrEmpty(department)) students = students.Where(s => s.Department == department);
            if (semester.HasValue) students = students.Where(s => s.Semester == semester.Value);

            var studentRows = students
                .Select(s => new { s.Id, s.RollNumber, s.User.FullName, s.Department, s.Semester })
                .ToList();

            var heldByGroup = _db.ClassSessions
                .GroupBy(cs => new { cs.Subject.Department, cs.Subject.Semester })
                .Select(g => new { g.Key.Department, g.Key.Semester, Count = g.Count() })
                .ToList()
                .ToDictionary(x => GroupKey(x.Department, x.Semester), x => x.Count, StringComparer.OrdinalIgnoreCase);

            var presentByStudent = _db.Attendances
                .Where(a => a.Status == AttendanceStatus.Present)
                .GroupBy(a => a.StudentId)
                .Select(g => new { StudentId = g.Key, Count = g.Count() })
                .ToDictionary(x => x.StudentId, x => x.Count);

            return studentRows.Select(s =>
            {
                int held, present;
                heldByGroup.TryGetValue(GroupKey(s.Department, s.Semester), out held);
                presentByStudent.TryGetValue(s.Id, out present);
                return new StudentAttendanceRow
                {
                    StudentId = s.Id,
                    RollNumber = s.RollNumber,
                    FullName = s.FullName,
                    Department = s.Department,
                    Semester = s.Semester,
                    SessionsHeld = held,
                    Present = Math.Min(present, held),
                    Percent = Percent(Math.Min(present, held), held)
                };
            }).ToList();
        }

        /// <summary>Present / expected across all sessions started in the given UTC range.</summary>
        public void SessionTotals(DateTime fromUtc, DateTime toUtc, out int sessions, out int present, out int expected)
        {
            var sessionRows = _db.ClassSessions
                .Where(cs => cs.StartedUtc >= fromUtc && cs.StartedUtc < toUtc)
                .Select(cs => new { cs.Id, cs.Subject.Department, cs.Subject.Semester })
                .ToList();

            var rosterSizes = RosterSizes();
            var ids = sessionRows.Select(s => s.Id).ToList();

            sessions = sessionRows.Count;
            expected = sessionRows.Sum(s =>
            {
                int size;
                rosterSizes.TryGetValue(GroupKey(s.Department, s.Semester), out size);
                return size;
            });
            present = ids.Count == 0
                ? 0
                : _db.Attendances.Count(a => ids.Contains(a.ClassSessionId) && a.Status == AttendanceStatus.Present &&
                                             a.Student.User.IsActive);
        }

        /// <summary>Number of active students per department/semester.</summary>
        public Dictionary<string, int> RosterSizes()
        {
            return _db.Students
                .Where(s => s.User.IsActive)
                .GroupBy(s => new { s.Department, s.Semester })
                .Select(g => new { g.Key.Department, g.Key.Semester, Count = g.Count() })
                .ToList()
                .ToDictionary(x => GroupKey(x.Department, x.Semester), x => x.Count, StringComparer.OrdinalIgnoreCase);
        }

        public static string GroupKey(string department, int semester)
        {
            return (department ?? "").Trim() + "|" + semester;
        }
    }
}
