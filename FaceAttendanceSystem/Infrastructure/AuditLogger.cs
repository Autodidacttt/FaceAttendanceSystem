using System;
using System.Web;
using FaceAttendanceSystem.Data;
using FaceAttendanceSystem.Models;

namespace FaceAttendanceSystem.Infrastructure
{
    /// <summary>Writes rows to the AuditLog table for security-relevant and data-changing actions.</summary>
    public static class AuditLogger
    {
        /// <summary>Adds an audit row to <paramref name="db"/>; it is saved with the caller's SaveChanges.</summary>
        public static void Add(AppDbContext db, HttpContextBase context, string action,
            string entityType = null, object entityId = null, string details = null,
            int? userIdOverride = null, string usernameOverride = null)
        {
            var principal = context?.User as AppPrincipal;

            db.AuditLogs.Add(new AuditLog
            {
                TimestampUtc = DateTime.UtcNow,
                UserId = userIdOverride ?? principal?.UserId,
                Username = Truncate(usernameOverride ?? principal?.Username, 50),
                Action = Truncate(action, 100),
                EntityType = Truncate(entityType, 50),
                EntityId = Truncate(entityId?.ToString(), 50),
                Details = Truncate(details, 1000),
                IpAddress = Truncate(context?.Request?.UserHostAddress, 45)
            });
        }

        /// <summary>Writes an audit row immediately using its own context.</summary>
        public static void Write(HttpContextBase context, string action,
            string entityType = null, object entityId = null, string details = null,
            int? userIdOverride = null, string usernameOverride = null)
        {
            using (var db = new AppDbContext())
            {
                Add(db, context, action, entityType, entityId, details, userIdOverride, usernameOverride);
                db.SaveChanges();
            }
        }

        private static string Truncate(string value, int max)
        {
            if (value == null) return null;
            return value.Length <= max ? value : value.Substring(0, max);
        }
    }
}
