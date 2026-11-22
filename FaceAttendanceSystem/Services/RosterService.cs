using System.Linq;
using FaceAttendanceSystem.Data;
using FaceAttendanceSystem.Models;

namespace FaceAttendanceSystem.Services
{
    /// <summary>
    /// A subject's roster is every active student in the subject's department and semester.
    /// </summary>
    public static class RosterService
    {
        public static IQueryable<Student> RosterFor(AppDbContext db, string department, int semester)
        {
            return db.Students.Where(s => s.User.IsActive && s.Department == department && s.Semester == semester);
        }

        public static IQueryable<Student> RosterFor(AppDbContext db, Subject subject)
        {
            return RosterFor(db, subject.Department, subject.Semester);
        }

        /// <summary>Sessions a student was expected to attend (sessions of subjects in their department/semester).</summary>
        public static IQueryable<ClassSession> SessionsFor(AppDbContext db, Student student)
        {
            return db.ClassSessions.Where(cs => cs.Subject.Department == student.Department &&
                                                cs.Subject.Semester == student.Semester);
        }
    }
}
