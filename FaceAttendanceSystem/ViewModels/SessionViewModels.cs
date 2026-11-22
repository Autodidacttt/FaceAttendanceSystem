using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using FaceAttendanceSystem.Infrastructure;
using FaceAttendanceSystem.Models;

namespace FaceAttendanceSystem.ViewModels
{
    public class TeacherSubjectCard
    {
        public int SubjectId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Department { get; set; }
        public int Semester { get; set; }
        public int RosterSize { get; set; }
        public int EnrolledCount { get; set; }
        public int SessionsHeld { get; set; }
        public int? ActiveSessionId { get; set; }
    }

    public class SessionRow
    {
        public int Id { get; set; }
        public string SubjectCode { get; set; }
        public string SubjectName { get; set; }
        public string Department { get; set; }
        public int Semester { get; set; }
        public string TeacherName { get; set; }
        public DateTime StartedUtc { get; set; }
        public DateTime? EndedUtc { get; set; }
        public int PresentCount { get; set; }
        public int RosterSize { get; set; }
        public bool IsActive => EndedUtc == null;
        public double Percent => RosterSize == 0 ? 0 : Math.Round(100.0 * Math.Min(PresentCount, RosterSize) / RosterSize, 1);

        /// <summary>Set when the history is filtered by roll number: "Present" or "Absent".</summary>
        public string StudentStatus { get; set; }
    }

    public class TeacherDashboardViewModel
    {
        public string TeacherName { get; set; }
        public List<TeacherSubjectCard> Subjects { get; set; } = new List<TeacherSubjectCard>();
        public List<SessionRow> ActiveSessions { get; set; } = new List<SessionRow>();
        public List<SessionRow> TodaySessions { get; set; } = new List<SessionRow>();
    }

    public class StartSessionViewModel
    {
        public List<TeacherSubjectCard> Subjects { get; set; } = new List<TeacherSubjectCard>();

        [Required(ErrorMessage = "Choose a subject.")]
        public int? SubjectId { get; set; }

        [StringLength(200)]
        public string Notes { get; set; }
    }

    public class RosterEntry
    {
        public int StudentId { get; set; }
        public string RollNumber { get; set; }
        public string FullName { get; set; }
        public bool HasFace { get; set; }
        public bool OnRoster { get; set; } = true;
        public int? AttendanceId { get; set; }
        public AttendanceStatus? Status { get; set; }
        public AttendanceMethod? Method { get; set; }
        public DateTime? MarkedUtc { get; set; }
        public double? Distance { get; set; }
        public bool HasSnapshot { get; set; }
        public bool IsPresent => Status == AttendanceStatus.Present;
    }

    public class SessionPageViewModel
    {
        public int SessionId { get; set; }
        public string SubjectCode { get; set; }
        public string SubjectName { get; set; }
        public string Department { get; set; }
        public int Semester { get; set; }
        public string TeacherName { get; set; }
        public string Notes { get; set; }
        public DateTime StartedUtc { get; set; }
        public DateTime? EndedUtc { get; set; }
        public bool IsActive => EndedUtc == null;
        public bool CanEdit { get; set; }
        public double MatchThreshold { get; set; }
        public List<RosterEntry> Roster { get; set; } = new List<RosterEntry>();
    }

    public class HistoryViewModel
    {
        public int? SubjectId { get; set; }

        [DataType(DataType.Date)]
        public DateTime? From { get; set; }

        [DataType(DataType.Date)]
        public DateTime? To { get; set; }

        public string Roll { get; set; }
        public string RollStudentName { get; set; }
        public string Message { get; set; }
        public IEnumerable<SelectListItem> SubjectOptions { get; set; }
        public PagedResult<SessionRow> Sessions { get; set; }
    }

    /// <summary>JSON body of POST /Attendance/Recognize.</summary>
    public class RecognizeRequest
    {
        public int SessionId { get; set; }
        public bool SaveSnapshots { get; set; }
        public List<RecognizeFace> Faces { get; set; }
    }

    public class RecognizeFace
    {
        /// <summary>Client-side tracking id, echoed back so the page can label the right box.</summary>
        public string TrackId { get; set; }

        /// <summary>Base64 of 128 little-endian float32 values.</summary>
        public string Descriptor { get; set; }

        /// <summary>Optional JPEG data URL of the face (only stored when snapshots are enabled).</summary>
        public string Snapshot { get; set; }
    }

    public class OverrideRequest
    {
        public int SessionId { get; set; }
        public int StudentId { get; set; }
        public bool Present { get; set; }
    }
}
