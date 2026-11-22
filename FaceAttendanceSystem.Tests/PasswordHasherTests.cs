using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using FaceAttendanceSystem.Services;
using FaceAttendanceSystem.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FaceAttendanceSystem.Tests
{
    [TestClass]
    public class PasswordHasherTests
    {
        [TestMethod]
        public void Hash_UsesPbkdf2Sha256Format_With100kIterations_16ByteSalt_32ByteHash()
        {
            var stored = PasswordHasher.Hash("Secret#123");
            var parts = stored.Split('$');

            Assert.AreEqual(5, parts.Length);
            Assert.AreEqual("PBKDF2", parts[0]);
            Assert.AreEqual("SHA256", parts[1]);
            Assert.AreEqual("100000", parts[2]);
            Assert.AreEqual(16, Convert.FromBase64String(parts[3]).Length);
            Assert.AreEqual(32, Convert.FromBase64String(parts[4]).Length);
        }

        [TestMethod]
        public void Verify_ReturnsTrue_ForTheCorrectPassword()
        {
            var stored = PasswordHasher.Hash("Correct Horse 9");
            Assert.IsTrue(PasswordHasher.Verify("Correct Horse 9", stored));
        }

        [TestMethod]
        public void Verify_ReturnsFalse_ForAWrongPassword()
        {
            var stored = PasswordHasher.Hash("Correct Horse 9");
            Assert.IsFalse(PasswordHasher.Verify("correct horse 9", stored));
            Assert.IsFalse(PasswordHasher.Verify("", stored));
            Assert.IsFalse(PasswordHasher.Verify(null, stored));
        }

        [TestMethod]
        public void Hash_UsesARandomSalt_SoTheSamePasswordHashesDifferently()
        {
            var a = PasswordHasher.Hash("Same#Password1");
            var b = PasswordHasher.Hash("Same#Password1");

            Assert.AreNotEqual(a, b);
            Assert.IsTrue(PasswordHasher.Verify("Same#Password1", a));
            Assert.IsTrue(PasswordHasher.Verify("Same#Password1", b));
        }

        [TestMethod]
        public void Verify_ReturnsFalse_WhenTheStoredHashWasTamperedWith()
        {
            var parts = PasswordHasher.Hash("Secret#123").Split('$');
            var hash = Convert.FromBase64String(parts[4]);
            hash[0] ^= 0xFF;
            parts[4] = Convert.ToBase64String(hash);

            Assert.IsFalse(PasswordHasher.Verify("Secret#123", string.Join("$", parts)));
        }

        [TestMethod]
        public void Verify_ReturnsFalse_ForMalformedStoredValues()
        {
            var malformed = new[]
            {
                null,
                "",
                "plain-text",
                "PBKDF2$SHA256$100000$onlyfour",
                "PBKDF2$SHA1$100000$AAAA$AAAA",
                "BCRYPT$SHA256$100000$AAAA$AAAA",
                "PBKDF2$SHA256$notanumber$AAAA$AAAA",
                "PBKDF2$SHA256$0$AAAA$AAAA",
                "PBKDF2$SHA256$100000$***$AAAA",
                "PBKDF2$SHA256$100000$$"
            };

            foreach (var stored in malformed)
            {
                Assert.IsFalse(PasswordHasher.Verify("anything", stored), "Should reject: " + (stored ?? "<null>"));
            }
        }

        [TestMethod]
        public void Verify_HonoursTheIterationCountStoredWithTheHash()
        {
            // A hash created with fewer iterations (e.g. by an older version) still verifies.
            var salt = new byte[16];
            new Random(42).NextBytes(salt);
            byte[] hash;
            using (var kdf = new System.Security.Cryptography.Rfc2898DeriveBytes("Legacy#1", salt, 1000,
                       System.Security.Cryptography.HashAlgorithmName.SHA256))
            {
                hash = kdf.GetBytes(32);
            }
            var stored = "PBKDF2$SHA256$1000$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);

            Assert.IsTrue(PasswordHasher.Verify("Legacy#1", stored));
            Assert.IsFalse(PasswordHasher.Verify("Legacy#2", stored));
        }

        [TestMethod]
        public void FixedTimeEquals_ComparesContentAndLength()
        {
            Assert.IsTrue(PasswordHasher.FixedTimeEquals(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 3 }));
            Assert.IsFalse(PasswordHasher.FixedTimeEquals(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 4 }));
            Assert.IsFalse(PasswordHasher.FixedTimeEquals(new byte[] { 1, 2, 3 }, new byte[] { 1, 2 }));
            Assert.IsFalse(PasswordHasher.FixedTimeEquals(null, new byte[] { 1 }));
            Assert.IsTrue(PasswordHasher.FixedTimeEquals(new byte[0], new byte[0]));
        }

        [TestMethod]
        public void GenerateTemporaryPassword_MeetsThePasswordPolicy_AndIsRandom()
        {
            var seen = new HashSet<string>();
            for (var i = 0; i < 200; i++)
            {
                var pw = PasswordHasher.GenerateTemporaryPassword();
                Assert.AreEqual(12, pw.Length);
                Assert.IsTrue(Regex.IsMatch(pw, PasswordPolicy.Pattern), "Policy failed for " + pw);
                Assert.IsTrue(Regex.IsMatch(pw, "[!@#$%*?]"), "No symbol in " + pw);
                Assert.IsTrue(seen.Add(pw), "Duplicate temporary password generated");
            }
        }
    }
}
