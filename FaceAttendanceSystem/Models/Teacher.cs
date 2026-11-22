using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FaceAttendanceSystem.Models
{
    public class Teacher
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public virtual User User { get; set; }

        [Required, StringLength(20)]
        public string EmployeeNumber { get; set; }

        [Required, StringLength(100)]
        public string Department { get; set; }

        public virtual ICollection<TeacherSubject> TeacherSubjects { get; set; } = new List<TeacherSubject>();
    }
}
