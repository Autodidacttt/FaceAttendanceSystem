using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FaceAttendanceSystem.Models
{
    /// <summary>One lecture / class meeting during which attendance is taken.</summary>
    public class ClassSession
    {
        public int Id { get; set; }

        public int SubjectId { get; set; }
        public virtual Subject Subject { get; set; }

        public int TeacherId { get; set; }
        public virtual Teacher Teacher { get; set; }

        public DateTime StartedUtc { get; set; }

        public DateTime? EndedUtc { get; set; }

        [StringLength(200)]
        public string Notes { get; set; }

        public bool IsActive => EndedUtc == null;

        public virtual ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    }
}
