using System;
using System.Linq;
using FaceAttendanceSystem.Data;
using FaceAttendanceSystem.Models;

namespace FaceAttendanceSystem.Services
{
    /// <summary>Creates accounts and resets passwords. Temporary passwords are returned once and never stored in clear.</summary>
    public static class UserAccountService
    {
        public static User CreateUser(AppDbContext db, string username, string email, string fullName, UserRole role,
            out string temporaryPassword)
        {
            temporaryPassword = PasswordHasher.GenerateTemporaryPassword();
            var user = new User
            {
                Username = username.Trim(),
                Email = email.Trim(),
                FullName = fullName.Trim(),
                Role = role,
                PasswordHash = PasswordHasher.Hash(temporaryPassword),
                IsActive = true,
                MustChangePassword = true,
                CreatedUtc = DateTime.UtcNow
            };
            db.Users.Add(user);
            return user;
        }

        /// <summary>Sets a new temporary password, forces a change at next sign-in and clears any lockout.</summary>
        public static string ResetPassword(User user)
        {
            var temporaryPassword = PasswordHasher.GenerateTemporaryPassword();
            user.PasswordHash = PasswordHasher.Hash(temporaryPassword);
            user.MustChangePassword = true;
            user.FailedLoginCount = 0;
            user.LockoutEndUtc = null;
            return temporaryPassword;
        }

        public static bool IsUsernameTaken(AppDbContext db, string username, int? exceptUserId = null)
        {
            var value = (username ?? "").Trim();
            return db.Users.Any(u => u.Username == value && (exceptUserId == null || u.Id != exceptUserId));
        }

        public static bool IsEmailTaken(AppDbContext db, string email, int? exceptUserId = null)
        {
            var value = (email ?? "").Trim();
            return db.Users.Any(u => u.Email == value && (exceptUserId == null || u.Id != exceptUserId));
        }
    }
}
