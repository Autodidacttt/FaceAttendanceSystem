using System;
using System.ComponentModel.DataAnnotations;

namespace FaceAttendanceSystem.Models
{
    /// <summary>
    /// At most one row per (session, student), enforced by a unique index.
    /// Roster students with no Present row for a session count as absent.
    /// </summary>
    public class Attendance
    {
        public int Id { get; set; }

        public int ClassSessionId { get; set; }
        public virtual ClassSession ClassSession { get; set; }

        public int StudentId { get; set; }
        public virtual Student Student { get; set; }

        public AttendanceStatus Status { get; set; }

        public AttendanceMethod Method { get; set; }

        public DateTime MarkedUtc { get; set; }

        /// <summary>Face distance of the match (face method only).</summary>
        public double? MatchDistance { get; set; }

        public int? MarkedByUserId { get; set; }

        /// <summary>Path relative to App_Data, e.g. "snapshots/12/34.jpg".</summary>
        [StringLength(260)]
        public string SnapshotPath { get; set; }
    }
}
