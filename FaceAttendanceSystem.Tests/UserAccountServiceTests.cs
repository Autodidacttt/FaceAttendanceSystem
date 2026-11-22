using System;
using FaceAttendanceSystem.Models;
using FaceAttendanceSystem.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FaceAttendanceSystem.Tests
{
    [TestClass]
    public class UserAccountServiceTests
    {
        [TestMethod]
        public void ResetPassword_DoesNotForceAPasswordChange_AndClearsLockout()
        {
            var user = new User
            {
                Username = "S001",
                PasswordHash = PasswordHasher.Hash("Old#Password1"),
                MustChangePassword = true,
                FailedLoginCount = 4,
                LockoutEndUtc = DateTime.UtcNow.AddMinutes(10)
            };

            var temp = UserAccountService.ResetPassword(user);

            Assert.IsFalse(user.MustChangePassword, "Users must not be forced to change their password.");
            Assert.AreEqual(0, user.FailedLoginCount);
            Assert.IsNull(user.LockoutEndUtc);
            Assert.IsTrue(PasswordHasher.Verify(temp, user.PasswordHash));
            Assert.IsFalse(PasswordHasher.Verify("Old#Password1", user.PasswordHash));
        }
    }
}
