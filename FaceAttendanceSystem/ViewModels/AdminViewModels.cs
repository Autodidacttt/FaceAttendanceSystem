using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using FaceAttendanceSystem.Infrastructure;
using FaceAttendanceSystem.Models;

namespace FaceAttendanceSystem.ViewModels
{
    public class StudentAttendanceRow
    {
        public int StudentId { get; set; }
        public string RollNumber { get; set; }
        public string FullName { get; set; }
        public string Department { get; set; }
        public int Semester { get; set; }
        public int SessionsHeld { get; set; }
        public int Present { get; set; }
        public int Absent => SessionsHeld - Present;
        public double Percent { get; set; }
    }

    public class AdminDashboardViewModel
    {
        public int StudentCount { get; set; }
        public int TeacherCount { get; set; }
        public int SubjectCount { get; set; }
        public int SessionsToday { get; set; }
        public int ActiveSessions { get; set; }
        public int TodayPresent { get; set; }
        public int TodayExpected { get; set; }
        public double TodayPercent { get; set; }
        public int StudentsWithoutFace { get; set; }
        public List<StudentAttendanceRow> LowestAttendance { get; set; } = new List<StudentAttendanceRow>();
        public List<AuditLog> RecentActivity { get; set; } = new List<AuditLog>();
    }

    public class StudentListItem
    {
        public int Id { get; set; }
        public string RollNumber { get; set; }
        public string FullName { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Department { get; set; }
        public int Semester { get; set; }
        public bool IsActive { get; set; }
        public bool IsLockedOut { get; set; }
        public int FaceSamples { get; set; }
    }

    public class StudentListViewModel
    {
        public string Query { get; set; }
        public PagedResult<StudentListItem> Students { get; set; }
    }

    public class StudentFormViewModel
    {
        public int? Id { get; set; }

        [Required, StringLength(100), Display(Name = "Full name")]
        public string FullName { get; set; }

        [Required, StringLength(20), Display(Name = "Roll number")]
        [RegularExpression(@"^[A-Za-z0-9\-_]+$", ErrorMessage = "Use letters, digits, '-' or '_' only.")]
        public string RollNumber { get; set; }

        [Required, StringLength(100), EmailAddress]
        public string Email { get; set; }

        [Required, StringLength(100)]
        public string Department { get; set; }

        [Range(1, 12)]
        public int Semester { get; set; } = 1;

        [Display(Name = "Account active")]
        public bool IsActive { get; set; } = true;
    }

    public class TeacherListItem
    {
        public int Id { get; set; }
        public string EmployeeNumber { get; set; }
        public string FullName { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Department { get; set; }
        public bool IsActive { get; set; }
        public bool IsLockedOut { get; set; }
        public List<string> SubjectCodes { get; set; } = new List<string>();
    }

    public class TeacherListViewModel
    {
        public string Query { get; set; }
        public PagedResult<TeacherListItem> Teachers { get; set; }
    }

    public class TeacherFormViewModel
    {
        public int? Id { get; set; }

        [Required, StringLength(100), Display(Name = "Full name")]
        public string FullName { get; set; }

        [Required, StringLength(50)]
        [RegularExpression(@"^[A-Za-z0-9.\-_]+$", ErrorMessage = "Use letters, digits, '.', '-' or '_' only.")]
        public string Username { get; set; }

        [Required, StringLength(100), EmailAddress]
        public string Email { get; set; }

        [Required, StringLength(20), Display(Name = "Employee number")]
        public string EmployeeNumber { get; set; }

        [Required, StringLength(100)]
        public string Department { get; set; }

        [Display(Name = "Account active")]
        public bool IsActive { get; set; } = true;
    }

    public class SubjectListItem
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Department { get; set; }
        public int Semester { get; set; }
        public int RosterSize { get; set; }
        public int SessionCount { get; set; }
        public List<string> Teachers { get; set; } = new List<string>();
    }

    public class SubjectFormViewModel
    {
        public int? Id { get; set; }

        [Required, StringLength(20)]
        [RegularExpression(@"^[A-Za-z0-9\-_]+$", ErrorMessage = "Use letters, digits, '-' or '_' only.")]
        public string Code { get; set; }

        [Required, StringLength(150)]
        public string Name { get; set; }

        [Required, StringLength(100)]
        public string Department { get; set; }

        [Range(1, 12)]
        public int Semester { get; set; } = 1;
    }

    public class AssignmentRow
    {
        public int Id { get; set; }
        public string TeacherName { get; set; }
        public string EmployeeNumber { get; set; }
        public string SubjectCode { get; set; }
        public string SubjectName { get; set; }
    }

    public class AssignmentsViewModel
    {
        public List<AssignmentRow> Assignments { get; set; } = new List<AssignmentRow>();

        [Required, Display(Name = "Teacher")]
        public int? TeacherId { get; set; }

        [Required, Display(Name = "Subject")]
        public int? SubjectId { get; set; }

        public IEnumerable<SelectListItem> TeacherOptions { get; set; }
        public IEnumerable<SelectListItem> SubjectOptions { get; set; }
    }

    public class CredentialsViewModel
    {
        public string FullName { get; set; }
        public string Username { get; set; }
        public string TemporaryPassword { get; set; }
        public string BackController { get; set; }
        public bool IsReset { get; set; }
    }

    public class SettingsViewModel
    {
        [Display(Name = "Face match threshold")]
        [Range(0.35, 0.60, ErrorMessage = "The threshold must be between 0.35 and 0.60.")]
        public double MatchThreshold { get; set; }
    }

    public class AuditListViewModel
    {
        public string Query { get; set; }
        public PagedResult<AuditLog> Entries { get; set; }
    }
}
