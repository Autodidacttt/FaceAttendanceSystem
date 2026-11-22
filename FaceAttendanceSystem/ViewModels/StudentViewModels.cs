using System;
using System.Collections.Generic;
using FaceAttendanceSystem.Models;

namespace FaceAttendanceSystem.ViewModels
{
    public class SubjectAttendanceRow
    {
        public int SubjectId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public int SessionsHeld { get; set; }
        public int Present { get; set; }
        public int Absent => SessionsHeld - Present;
        public double Percent { get; set; }
    }

    public class StudentSessionEntry
    {
        public int SessionId { get; set; }
        public string SubjectCode { get; set; }
        public string SubjectName { get; set; }
        public DateTime StartedUtc { get; set; }
        public bool Present { get; set; }
        public AttendanceMethod? Method { get; set; }
        public DateTime? MarkedUtc { get; set; }
    }

    public class StudentDashboardViewModel
    {
        public string FullName { get; set; }
        public string RollNumber { get; set; }
        public string Department { get; set; }
        public int Semester { get; set; }
        public int SessionsHeld { get; set; }
        public int Present { get; set; }
        public double Percent { get; set; }
        public bool FaceEnrolled { get; set; }
        public List<SubjectAttendanceRow> Subjects { get; set; } = new List<SubjectAttendanceRow>();
        public List<StudentSessionEntry> Recent { get; set; } = new List<StudentSessionEntry>();
    }

    public class StudentProfileViewModel
    {
        public string FullName { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string RollNumber { get; set; }
        public string Department { get; set; }
        public int Semester { get; set; }
        public bool FaceEnrolled { get; set; }
        public int FaceSamples { get; set; }
        public DateTime? EnrolledUtc { get; set; }
        public DateTime? LastLoginUtc { get; set; }
    }
}
