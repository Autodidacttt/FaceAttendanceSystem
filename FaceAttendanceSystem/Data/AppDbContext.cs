using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using FaceAttendanceSystem.Models;

namespace FaceAttendanceSystem.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext() : base("name=FaceAttendanceDB")
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Teacher> Teachers { get; set; }
        public DbSet<Subject> Subjects { get; set; }
        public DbSet<TeacherSubject> TeacherSubjects { get; set; }
        public DbSet<FaceEnrollment> FaceEnrollments { get; set; }
        public DbSet<ClassSession> ClassSessions { get; set; }
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<AppSetting> AppSettings { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            // Deletes are handled explicitly in code; cascading paths
            // (Attendance -> Session -> Subject and Attendance -> Student) would conflict in SQL Server.
            modelBuilder.Conventions.Remove<OneToManyCascadeDeleteConvention>();
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();

            modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

            modelBuilder.Entity<Student>().HasIndex(s => s.RollNumber).IsUnique();
            modelBuilder.Entity<Student>().HasIndex(s => s.UserId).IsUnique();
            modelBuilder.Entity<Student>().HasIndex(s => new { s.Department, s.Semester });
            modelBuilder.Entity<Student>().HasRequired(s => s.User).WithMany().HasForeignKey(s => s.UserId);

            modelBuilder.Entity<Teacher>().HasIndex(t => t.EmployeeNumber).IsUnique();
            modelBuilder.Entity<Teacher>().HasIndex(t => t.UserId).IsUnique();
            modelBuilder.Entity<Teacher>().HasRequired(t => t.User).WithMany().HasForeignKey(t => t.UserId);

            modelBuilder.Entity<Subject>().HasIndex(s => s.Code).IsUnique();

            modelBuilder.Entity<TeacherSubject>()
                .HasIndex(ts => new { ts.TeacherId, ts.SubjectId }).IsUnique();

            modelBuilder.Entity<Attendance>()
                .HasIndex(a => new { a.ClassSessionId, a.StudentId })
                .IsUnique()
                .HasName("IX_Attendance_Session_Student");

            modelBuilder.Entity<ClassSession>().HasIndex(cs => cs.StartedUtc);

            modelBuilder.Entity<AuditLog>().HasIndex(a => a.TimestampUtc);

            base.OnModelCreating(modelBuilder);
        }
    }
}
