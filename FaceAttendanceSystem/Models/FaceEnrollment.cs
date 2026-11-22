using System;
using System.ComponentModel.DataAnnotations;

namespace FaceAttendanceSystem.Models
{
    /// <summary>One 128-value face descriptor sample for a student (no image is stored).</summary>
    public class FaceEnrollment
    {
        public int Id { get; set; }

        public int StudentId { get; set; }
        public virtual Student Student { get; set; }

        /// <summary>128 little-endian float32 values (512 bytes).</summary>
        [Required, MaxLength(512)]
        public byte[] Descriptor { get; set; }

        /// <summary>Face detector confidence when the sample was captured (0..1).</summary>
        public double DetectionScore { get; set; }

        public DateTime CreatedUtc { get; set; }

        public int? CreatedByUserId { get; set; }
    }
}
