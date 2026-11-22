using System;
using System.Web;
using System.Web.Security;
using FaceAttendanceSystem.Models;

namespace FaceAttendanceSystem.Infrastructure
{
    /// <summary>Issues the forms-authentication cookie carrying user id, role and display name.</summary>
    public static class AuthCookie
    {
        public static void SignIn(HttpContextBase context, User user)
        {
            var now = DateTime.Now;
            var ticket = new FormsAuthenticationTicket(
                1,
                user.Username,
                now,
                now.Add(FormsAuthentication.Timeout),
                false,
                AppPrincipal.SerializeUserData(user),
                FormsAuthentication.FormsCookiePath);

            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, FormsAuthentication.Encrypt(ticket))
            {
                HttpOnly = true,
                Secure = FormsAuthentication.RequireSSL,
                Path = FormsAuthentication.FormsCookiePath
            };
            if (!string.IsNullOrEmpty(FormsAuthentication.CookieDomain)) cookie.Domain = FormsAuthentication.CookieDomain;

            context.Response.Cookies.Add(cookie);
        }

        public static void SignOut(HttpContextBase context)
        {
            FormsAuthentication.SignOut();
            if (context.Session != null) context.Session.Abandon();
        }
    }
}
