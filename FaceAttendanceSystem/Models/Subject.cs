using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FaceAttendanceSystem.Models
{
    public class Subject
    {
        public int Id { get; set; }

        [Required, StringLength(20)]
        public string Code { get; set; }

        [Required, StringLength(150)]
        public string Name { get; set; }

        /// <summary>Students in this department and semester make up the subject's roster.</summary>
        [Required, StringLength(100)]
        public string Department { get; set; }

        [Range(1, 12)]
        public int Semester { get; set; }

        public virtual ICollection<TeacherSubject> TeacherSubjects { get; set; } = new List<TeacherSubject>();
    }
}
