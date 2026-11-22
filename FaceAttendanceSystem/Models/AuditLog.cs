using System;
using System.ComponentModel.DataAnnotations;

namespace FaceAttendanceSystem.Models
{
    public class AuditLog
    {
        public long Id { get; set; }

        public DateTime TimestampUtc { get; set; }

        public int? UserId { get; set; }

        [StringLength(50)]
        public string Username { get; set; }

        [Required, StringLength(100)]
        public string Action { get; set; }

        [StringLength(50)]
        public string EntityType { get; set; }

        [StringLength(50)]
        public string EntityId { get; set; }

        [StringLength(1000)]
        public string Details { get; set; }

        [StringLength(45)]
        public string IpAddress { get; set; }
    }
}
