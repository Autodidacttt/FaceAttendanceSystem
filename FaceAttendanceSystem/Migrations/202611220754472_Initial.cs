namespace FaceAttendanceSystem.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Initial : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.AppSetting",
                c => new
                    {
                        Key = c.String(nullable: false, maxLength: 100),
                        Value = c.String(nullable: false, maxLength: 500),
                    })
                .PrimaryKey(t => t.Key);
            
            CreateTable(
                "dbo.Attendance",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ClassSessionId = c.Int(nullable: false),
                        StudentId = c.Int(nullable: false),
                        Status = c.Int(nullable: false),
                        Method = c.Int(nullable: false),
                        MarkedUtc = c.DateTime(nullable: false),
                        MatchDistance = c.Double(),
                        MarkedByUserId = c.Int(),
                        SnapshotPath = c.String(maxLength: 260),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ClassSession", t => t.ClassSessionId)
                .ForeignKey("dbo.Student", t => t.StudentId)
                .Index(t => new { t.ClassSessionId, t.StudentId }, unique: true, name: "IX_Attendance_Session_Student");
            
            CreateTable(
                "dbo.ClassSession",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        SubjectId = c.Int(nullable: false),
                        TeacherId = c.Int(nullable: false),
                        StartedUtc = c.DateTime(nullable: false),
                        EndedUtc = c.DateTime(),
                        Notes = c.String(maxLength: 200),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Subject", t => t.SubjectId)
                .ForeignKey("dbo.Teacher", t => t.TeacherId)
                .Index(t => t.SubjectId)
                .Index(t => t.TeacherId)
                .Index(t => t.StartedUtc);
            
            CreateTable(
                "dbo.Subject",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Code = c.String(nullable: false, maxLength: 20),
                        Name = c.String(nullable: false, maxLength: 150),
                        Department = c.String(nullable: false, maxLength: 100),
                        Semester = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.Code, unique: true);
            
            CreateTable(
                "dbo.TeacherSubject",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        TeacherId = c.Int(nullable: false),
                        SubjectId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Subject", t => t.SubjectId)
                .ForeignKey("dbo.Teacher", t => t.TeacherId)
                .Index(t => new { t.TeacherId, t.SubjectId }, unique: true);
            
            CreateTable(
                "dbo.Teacher",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        UserId = c.Int(nullable: false),
                        EmployeeNumber = c.String(nullable: false, maxLength: 20),
                        Department = c.String(nullable: false, maxLength: 100),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.User", t => t.UserId)
                .Index(t => t.UserId, unique: true)
                .Index(t => t.EmployeeNumber, unique: true);
            
            CreateTable(
                "dbo.User",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Username = c.String(nullable: false, maxLength: 50),
                        Email = c.String(nullable: false, maxLength: 100),
                        FullName = c.String(nullable: false, maxLength: 100),
                        PasswordHash = c.String(nullable: false, maxLength: 200),
                        Role = c.Int(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        MustChangePassword = c.Boolean(nullable: false),
                        FailedLoginCount = c.Int(nullable: false),
                        LockoutEndUtc = c.DateTime(),
                        CreatedUtc = c.DateTime(nullable: false),
                        LastLoginUtc = c.DateTime(),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.Username, unique: true)
                .Index(t => t.Email, unique: true);
            
            CreateTable(
                "dbo.Student",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        UserId = c.Int(nullable: false),
                        RollNumber = c.String(nullable: false, maxLength: 20),
                        Department = c.String(nullable: false, maxLength: 100),
                        Semester = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.User", t => t.UserId)
                .Index(t => t.UserId, unique: true)
                .Index(t => t.RollNumber, unique: true)
                .Index(t => new { t.Department, t.Semester });
            
            CreateTable(
                "dbo.FaceEnrollment",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        StudentId = c.Int(nullable: false),
                        Descriptor = c.Binary(nullable: false, maxLength: 512),
                        DetectionScore = c.Double(nullable: false),
                        CreatedUtc = c.DateTime(nullable: false),
                        CreatedByUserId = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Student", t => t.StudentId)
                .Index(t => t.StudentId);
            
            CreateTable(
                "dbo.AuditLog",
                c => new
                    {
                        Id = c.Long(nullable: false, identity: true),
                        TimestampUtc = c.DateTime(nullable: false),
                        UserId = c.Int(),
                        Username = c.String(maxLength: 50),
                        Action = c.String(nullable: false, maxLength: 100),
                        EntityType = c.String(maxLength: 50),
                        EntityId = c.String(maxLength: 50),
                        Details = c.String(maxLength: 1000),
                        IpAddress = c.String(maxLength: 45),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.TimestampUtc);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Student", "UserId", "dbo.User");
            DropForeignKey("dbo.FaceEnrollment", "StudentId", "dbo.Student");
            DropForeignKey("dbo.Attendance", "StudentId", "dbo.Student");
            DropForeignKey("dbo.ClassSession", "TeacherId", "dbo.Teacher");
            DropForeignKey("dbo.ClassSession", "SubjectId", "dbo.Subject");
            DropForeignKey("dbo.Teacher", "UserId", "dbo.User");
            DropForeignKey("dbo.TeacherSubject", "TeacherId", "dbo.Teacher");
            DropForeignKey("dbo.TeacherSubject", "SubjectId", "dbo.Subject");
            DropForeignKey("dbo.Attendance", "ClassSessionId", "dbo.ClassSession");
            DropIndex("dbo.AuditLog", new[] { "TimestampUtc" });
            DropIndex("dbo.FaceEnrollment", new[] { "StudentId" });
            DropIndex("dbo.Student", new[] { "Department", "Semester" });
            DropIndex("dbo.Student", new[] { "RollNumber" });
            DropIndex("dbo.Student", new[] { "UserId" });
            DropIndex("dbo.User", new[] { "Email" });
            DropIndex("dbo.User", new[] { "Username" });
            DropIndex("dbo.Teacher", new[] { "EmployeeNumber" });
            DropIndex("dbo.Teacher", new[] { "UserId" });
            DropIndex("dbo.TeacherSubject", new[] { "TeacherId", "SubjectId" });
            DropIndex("dbo.Subject", new[] { "Code" });
            DropIndex("dbo.ClassSession", new[] { "StartedUtc" });
            DropIndex("dbo.ClassSession", new[] { "TeacherId" });
            DropIndex("dbo.ClassSession", new[] { "SubjectId" });
            DropIndex("dbo.Attendance", "IX_Attendance_Session_Student");
            DropTable("dbo.AuditLog");
            DropTable("dbo.FaceEnrollment");
            DropTable("dbo.Student");
            DropTable("dbo.User");
            DropTable("dbo.Teacher");
            DropTable("dbo.TeacherSubject");
            DropTable("dbo.Subject");
            DropTable("dbo.ClassSession");
            DropTable("dbo.Attendance");
            DropTable("dbo.AppSetting");
        }
    }
}
