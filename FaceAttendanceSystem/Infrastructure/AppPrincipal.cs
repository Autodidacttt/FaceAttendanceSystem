using System;
using System.Globalization;
using System.Security.Principal;
using FaceAttendanceSystem.Models;

namespace FaceAttendanceSystem.Infrastructure
{
    /// <summary>
    /// The signed-in user, rebuilt on every request from the encrypted forms-auth ticket
    /// (see Global.asax Application_PostAuthenticateRequest).
    /// </summary>
    public class AppPrincipal : IPrincipal
    {
        public AppPrincipal(IIdentity identity, int userId, UserRole role, string fullName)
        {
            Identity = identity;
            UserId = userId;
            Role = role;
            FullName = fullName;
        }

        public IIdentity Identity { get; }
        public int UserId { get; }
        public UserRole Role { get; }
        public string FullName { get; }
        public string Username => Identity.Name;

        public bool IsInRole(string role)
        {
            return string.Equals(Role.ToString(), role, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Ticket user data format: "1|userId|role|fullName" (full name last because it may contain '|').</summary>
        public static string SerializeUserData(User user)
        {
            return "1|" + user.Id.ToString(CultureInfo.InvariantCulture) + "|" + (int)user.Role + "|" + user.FullName;
        }

        public static bool TryParseUserData(string data, out int userId, out UserRole role, out string fullName)
        {
            userId = 0;
            role = 0;
            fullName = null;
            if (string.IsNullOrEmpty(data)) return false;

            var parts = data.Split(new[] { '|' }, 4);
            if (parts.Length != 4 || parts[0] != "1") return false;

            int roleValue;
            if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out userId) || userId <= 0) return false;
            if (!int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out roleValue) ||
                !Enum.IsDefined(typeof(UserRole), roleValue))
            {
                return false;
            }

            role = (UserRole)roleValue;
            fullName = parts[3];
            return true;
        }
    }
}
