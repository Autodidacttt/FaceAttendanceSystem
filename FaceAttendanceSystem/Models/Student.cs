using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FaceAttendanceSystem.Models
{
    /// <summary>
    /// A student. The student's class roster is every subject with the same Department and Semester.
    /// </summary>
    public class Student
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public virtual User User { get; set; }

        [Required, StringLength(20)]
        public string RollNumber { get; set; }

        [Required, StringLength(100)]
        public string Department { get; set; }

        [Range(1, 12)]
        public int Semester { get; set; }

        public virtual ICollection<FaceEnrollment> FaceEnrollments { get; set; } = new List<FaceEnrollment>();
        public virtual ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    }
}
