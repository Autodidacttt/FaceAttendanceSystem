using System;
using FaceAttendanceSystem.Models;
using FaceAttendanceSystem.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FaceAttendanceSystem.Tests
{
    /// <summary>The duplicate-attendance rule: one record per student per session.</summary>
    [TestClass]
    public class AttendanceRulesTests
    {
        [TestMethod]
        public void FirstRecognition_CreatesAPresentRecord()
        {
            Assert.AreEqual(FaceMarkDecision.CreatePresent, AttendanceRules.DecideFaceMark(null));
        }

        [TestMethod]
        public void RecognisingAPresentStudentAgain_IsADuplicate_AndChangesNothing()
        {
            var byFace = new Attendance { Status = AttendanceStatus.Present, Method = AttendanceMethod.Face };
            var byHand = new Attendance { Status = AttendanceStatus.Present, Method = AttendanceMethod.Manual };

            Assert.AreEqual(FaceMarkDecision.AlreadyPresent, AttendanceRules.DecideFaceMark(byFace));
            Assert.AreEqual(FaceMarkDecision.AlreadyPresent, AttendanceRules.DecideFaceMark(byHand));
        }

        [TestMethod]
        public void TeachersManualAbsent_IsNotOverriddenByTheCamera()
        {
            var manualAbsent = new Attendance { Status = AttendanceStatus.Absent, Method = AttendanceMethod.Manual };
            Assert.AreEqual(FaceMarkDecision.KeepManualAbsent, AttendanceRules.DecideFaceMark(manualAbsent));
        }

        [TestMethod]
        public void RepeatedRecognitions_NeverProduceMoreThanOneRecord()
        {
            // Simulate the service loop: apply the rule to a stream of recognitions of the same student.
            Attendance record = null;
            var created = 0;
            for (var i = 0; i < 10; i++)
            {
                if (AttendanceRules.DecideFaceMark(record) == FaceMarkDecision.CreatePresent)
                {
                    record = new Attendance { Status = AttendanceStatus.Present, Method = AttendanceMethod.Face };
                    created++;
                }
            }
            Assert.AreEqual(1, created);
        }

        [TestMethod]
        public void FaceMarking_IsOnlyAllowedWhileTheSessionIsRunning()
        {
            Assert.IsTrue(AttendanceRules.CanMarkByFace(new ClassSession { StartedUtc = DateTime.UtcNow }));
            Assert.IsFalse(AttendanceRules.CanMarkByFace(new ClassSession { StartedUtc = DateTime.UtcNow, EndedUtc = DateTime.UtcNow }));
            Assert.IsFalse(AttendanceRules.CanMarkByFace(null));
        }

        [TestMethod]
        public void SnapshotDecoder_AcceptsOnlySmallJpegDataUrls()
        {
            byte[] jpeg;
            var validJpeg = "data:image/jpeg;base64," + Convert.ToBase64String(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3 });
            var png = "data:image/png;base64," + Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
            var fakeJpeg = "data:image/jpeg;base64," + Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 });
            var huge = "data:image/jpeg;base64," + Convert.ToBase64String(new byte[AttendanceService.MaxSnapshotBytes + 10]);

            Assert.IsTrue(AttendanceService.TryDecodeSnapshot(validJpeg, out jpeg));
            Assert.IsFalse(AttendanceService.TryDecodeSnapshot(png, out jpeg));
            Assert.IsFalse(AttendanceService.TryDecodeSnapshot(fakeJpeg, out jpeg));
            Assert.IsFalse(AttendanceService.TryDecodeSnapshot(huge, out jpeg));
            Assert.IsFalse(AttendanceService.TryDecodeSnapshot(null, out jpeg));
        }
    }
}
