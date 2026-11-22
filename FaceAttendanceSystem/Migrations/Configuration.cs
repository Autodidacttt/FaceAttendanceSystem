using System;
using System.Data.Entity.Migrations;
using System.Globalization;
using System.Linq;
using FaceAttendanceSystem.Data;
using FaceAttendanceSystem.Models;
using FaceAttendanceSystem.Services;

namespace FaceAttendanceSystem.Migrations
{
    internal sealed class Configuration : DbMigrationsConfiguration<AppDbContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
            MigrationsDirectory = "Migrations";
        }

        /// <summary>
        /// Idempotent seed: only inserts rows that do not exist yet, so existing passwords and
        /// edits are never overwritten. Demo accounts are not forced to change their password.
        /// </summary>
        protected override void Seed(AppDbContext db)
        {
            var now = DateTime.UtcNow;

            if (!db.AppSettings.Any(s => s.Key == AppSetting.MatchThresholdKey))
            {
                db.AppSettings.Add(new AppSetting
                {
                    Key = AppSetting.MatchThresholdKey,
                    Value = 0.5.ToString(CultureInfo.InvariantCulture)
                });
            }

            EnsureUser(db, "admin", "admin@faceattendance.local", "System Administrator", "Admin@123", UserRole.Admin, now);
            db.SaveChanges();

            var subjects = new[]
            {
                new Subject { Code = "CS101", Name = "Programming Fundamentals", Department = "Computer Science", Semester = 1 },
                new Subject { Code = "CS102", Name = "Discrete Mathematics", Department = "Computer Science", Semester = 1 },
                new Subject { Code = "CS103", Name = "Digital Logic Design", Department = "Computer Science", Semester = 1 }
            };
            foreach (var subject in subjects)
            {
                if (!db.Subjects.Any(s => s.Code == subject.Code)) db.Subjects.Add(subject);
            }
            db.SaveChanges();

            var teacherUser = EnsureUser(db, "teacher1", "teacher1@faceattendance.local", "Demo Teacher", "Teacher@123", UserRole.Teacher, now);
            db.SaveChanges();
            var teacher = db.Teachers.FirstOrDefault(t => t.UserId == teacherUser.Id);
            if (teacher == null)
            {
                teacher = new Teacher { UserId = teacherUser.Id, EmployeeNumber = "T001", Department = "Computer Science" };
                db.Teachers.Add(teacher);
                db.SaveChanges();
            }

            foreach (var code in new[] { "CS101", "CS102" })
            {
                var subjectId = db.Subjects.Where(s => s.Code == code).Select(s => s.Id).First();
                if (!db.TeacherSubjects.Any(ts => ts.TeacherId == teacher.Id && ts.SubjectId == subjectId))
                {
                    db.TeacherSubjects.Add(new TeacherSubject { TeacherId = teacher.Id, SubjectId = subjectId });
                }
            }
            db.SaveChanges();

            var students = new[]
            {
                new { Roll = "S001", Name = "Demo Student" },
                new { Roll = "S002", Name = "Aarav Sharma" },
                new { Roll = "S003", Name = "Priya Thapa" },
                new { Roll = "S004", Name = "Rohan Karki" },
                new { Roll = "S005", Name = "Sita Gurung" }
            };
            foreach (var s in students)
            {
                var user = EnsureUser(db, s.Roll, s.Roll.ToLowerInvariant() + "@faceattendance.local", s.Name, "Student@123", UserRole.Student, now);
                db.SaveChanges();
                if (!db.Students.Any(x => x.UserId == user.Id))
                {
                    db.Students.Add(new Student
                    {
                        UserId = user.Id,
                        RollNumber = s.Roll,
                        Department = "Computer Science",
                        Semester = 1
                    });
                }
            }
            db.SaveChanges();
        }

        private static User EnsureUser(AppDbContext db, string username, string email, string fullName,
            string password, UserRole role, DateTime now)
        {
            var user = db.Users.FirstOrDefault(u => u.Username == username);
            if (user != null) return user;

            user = new User
            {
                Username = username,
                Email = email,
                FullName = fullName,
                PasswordHash = PasswordHasher.Hash(password),
                Role = role,
                IsActive = true,
                MustChangePassword = false,
                CreatedUtc = now
            };
            db.Users.Add(user);
            return user;
        }
    }
}
