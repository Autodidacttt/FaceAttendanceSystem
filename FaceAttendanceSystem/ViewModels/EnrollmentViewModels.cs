using System.Collections.Generic;
using FaceAttendanceSystem.Infrastructure;

namespace FaceAttendanceSystem.ViewModels
{
    public class EnrollmentListViewModel
    {
        public string Query { get; set; }
        public bool OnlyMissing { get; set; }
        public PagedResult<StudentListItem> Students { get; set; }
    }

    public class EnrollPageViewModel
    {
        public int StudentId { get; set; }
        public string RollNumber { get; set; }
        public string FullName { get; set; }
        public string Department { get; set; }
        public int Semester { get; set; }
        public bool IsActive { get; set; }
        public int ExistingSamples { get; set; }
        public int RequiredSamples { get; set; }
        public int MaxSamples { get; set; }
    }

    /// <summary>JSON body of POST /Enrollment/Save.</summary>
    public class EnrollmentSaveRequest
    {
        public int StudentId { get; set; }
        public bool Force { get; set; }
        public List<EnrollmentSample> Samples { get; set; }
    }

    public class EnrollmentSample
    {
        /// <summary>Base64 of 128 little-endian float32 values.</summary>
        public string Descriptor { get; set; }

        /// <summary>Detector confidence 0..1.</summary>
        public double Score { get; set; }
    }
}
