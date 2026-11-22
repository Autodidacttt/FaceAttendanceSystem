using System;
using System.ComponentModel.DataAnnotations;

namespace FaceAttendanceSystem.Models
{
    /// <summary>A login account. Students and teachers each have one (1:1).</summary>
    public class User
    {
        public int Id { get; set; }

        [Required, StringLength(50)]
        public string Username { get; set; }

        [Required, StringLength(100)]
        public string Email { get; set; }

        [Required, StringLength(100)]
        public string FullName { get; set; }

        /// <summary>Format: PBKDF2$SHA256$iterations$saltBase64$hashBase64 (see PasswordHasher).</summary>
        [Required, StringLength(200)]
        public string PasswordHash { get; set; }

        public UserRole Role { get; set; }

        public bool IsActive { get; set; }

        public bool MustChangePassword { get; set; }

        public int FailedLoginCount { get; set; }

        public DateTime? LockoutEndUtc { get; set; }

        public DateTime CreatedUtc { get; set; }

        public DateTime? LastLoginUtc { get; set; }
    }
}
