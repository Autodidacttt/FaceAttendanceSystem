using System;
using System.Linq;
using System.Web.Mvc;
using FaceAttendanceSystem.Infrastructure;
using FaceAttendanceSystem.Services;
using FaceAttendanceSystem.ViewModels;

namespace FaceAttendanceSystem.Controllers
{
    public class AccountController : BaseController
    {
        public const int MaxFailedAttempts = 5;
        public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
        private const string InvalidCredentials = "Invalid username or password.";

        // Used to spend the same PBKDF2 time for unknown usernames as for real ones.
        private static readonly Lazy<string> DummyHash =
            new Lazy<string>(() => PasswordHasher.Hash(Guid.NewGuid().ToString()));

        [AllowAnonymous, HttpGet]
        public ActionResult Login(string returnUrl)
        {
            if (User.Identity.IsAuthenticated) return RedirectToAction("Index", "Home");
            ViewBag.ReturnUrl = returnUrl;
            return View(new LoginViewModel());
        }

        [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model, string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            if (!ModelState.IsValid) return View(model);

            var username = model.Username.Trim();
            var user = Db.Users.FirstOrDefault(u => u.Username == username);
            var now = DateTime.UtcNow;

            if (user == null)
            {
                PasswordHasher.Verify(model.Password, DummyHash.Value);
                AuditLogger.Write(HttpContext, "LoginFailed", "User", null, "Unknown username", null, username);
                return LoginError(model, InvalidCredentials);
            }

            if (user.LockoutEndUtc.HasValue && user.LockoutEndUtc.Value > now)
            {
                var minutes = (int)Math.Ceiling((user.LockoutEndUtc.Value - now).TotalMinutes);
                AuditLogger.Write(HttpContext, "LoginBlockedLockedOut", "User", user.Id, null, user.Id, user.Username);
                return LoginError(model, $"This account is locked after too many failed sign-ins. Try again in {minutes} minute(s).");
            }

            if (!PasswordHasher.Verify(model.Password, user.PasswordHash))
            {
                user.FailedLoginCount++;
                var details = $"Failed attempt {user.FailedLoginCount} of {MaxFailedAttempts}";
                if (user.FailedLoginCount >= MaxFailedAttempts)
                {
                    user.LockoutEndUtc = now.Add(LockoutDuration);
                    user.FailedLoginCount = 0;
                    AuditLogger.Add(Db, HttpContext, "AccountLocked", "User", user.Id,
                        $"Locked for {LockoutDuration.TotalMinutes} minutes", user.Id, user.Username);
                }
                AuditLogger.Add(Db, HttpContext, "LoginFailed", "User", user.Id, details, user.Id, user.Username);
                Db.SaveChanges();
                return LoginError(model, InvalidCredentials);
            }

            if (!user.IsActive)
            {
                AuditLogger.Write(HttpContext, "LoginFailedInactive", "User", user.Id, null, user.Id, user.Username);
                return LoginError(model, InvalidCredentials);
            }

            user.FailedLoginCount = 0;
            user.LockoutEndUtc = null;
            user.LastLoginUtc = now;
            AuditLogger.Add(Db, HttpContext, "LoginSucceeded", "User", user.Id, null, user.Id, user.Username);
            Db.SaveChanges();

            AuthCookie.SignIn(HttpContext, user);

            if (user.MustChangePassword) return RedirectToAction("ChangePassword");
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            Audit("Logout", "User", CurrentUserId);
            Db.SaveChanges();
            AuthCookie.SignOut(HttpContext);
            return RedirectToAction("Login");
        }

        [HttpGet]
        public ActionResult ChangePassword()
        {
            var user = Db.Users.Find(CurrentUserId);
            if (user == null) return RedirectToAction("Login");
            return View(new ChangePasswordViewModel { IsForced = user.MustChangePassword });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ChangePassword(ChangePasswordViewModel model)
        {
            var user = Db.Users.Find(CurrentUserId);
            if (user == null) return RedirectToAction("Login");
            model.IsForced = user.MustChangePassword;

            if (!ModelState.IsValid) return View(model);

            if (!PasswordHasher.Verify(model.CurrentPassword, user.PasswordHash))
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "The current password is incorrect.");
                Audit("PasswordChangeFailed", "User", user.Id, "Wrong current password");
                Db.SaveChanges();
                return View(model);
            }

            if (model.NewPassword == model.CurrentPassword)
            {
                ModelState.AddModelError(nameof(model.NewPassword), "The new password must be different from the current one.");
                return View(model);
            }

            user.PasswordHash = PasswordHasher.Hash(model.NewPassword);
            user.MustChangePassword = false;
            Audit("PasswordChanged", "User", user.Id);
            Db.SaveChanges();

            Flash("Your password has been changed.");
            return RedirectToAction("Index", "Home");
        }

        private ActionResult LoginError(LoginViewModel model, string message)
        {
            model.Password = null;
            ModelState.AddModelError(string.Empty, message);
            return View(model);
        }
    }
}
