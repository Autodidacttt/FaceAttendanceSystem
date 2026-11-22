using System;
using FaceAttendanceSystem.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FaceAttendanceSystem.Tests.TestVectors;

namespace FaceAttendanceSystem.Tests
{
    [TestClass]
    public class FaceMatcherTests
    {
        private const double Threshold = 0.5;
        private const double Tolerance = 1e-6;

        [TestMethod]
        public void Distance_IsEuclidean()
        {
            var a = Zero();
            var b = Zero();
            b[0] = 3;
            b[1] = 4;
            Assert.AreEqual(5.0, FaceMatcher.Distance(a, b), Tolerance);
            Assert.AreEqual(0.0, FaceMatcher.Distance(b, b), Tolerance);
            Assert.AreEqual(FaceMatcher.Distance(a, b), FaceMatcher.Distance(b, a), Tolerance);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void Distance_RejectsDescriptorsOfDifferentLength()
        {
            FaceMatcher.Distance(new float[128], new float[127]);
        }

        [TestMethod]
        public void Match_PicksTheClosestStudent_WhenUnderThresholdWithClearMargin()
        {
            var probe = Zero();
            var result = FaceMatcher.Match(probe, new[]
            {
                Candidate(1, Axis(0, 0.30f)),
                Candidate(2, Axis(1, 0.45f))
            }, Threshold);

            Assert.AreEqual(MatchOutcome.Matched, result.Outcome);
            Assert.IsTrue(result.IsMatch);
            Assert.AreEqual(1, result.Best.StudentId);
            Assert.AreEqual(0.30, result.BestDistance, Tolerance);
            Assert.AreEqual(0.45, result.SecondBestDistance.Value, Tolerance);
        }

        [TestMethod]
        public void Match_ReturnsUnknown_WhenEveryoneIsAboveTheThreshold()
        {
            var result = FaceMatcher.Match(Zero(), new[]
            {
                Candidate(1, Axis(0, 0.55f)),
                Candidate(2, Axis(1, 0.90f))
            }, Threshold);

            Assert.AreEqual(MatchOutcome.AboveThreshold, result.Outcome);
            Assert.IsFalse(result.IsMatch);
        }

        [TestMethod]
        public void Match_DistanceExactlyAtThreshold_Matches()
        {
            var result = FaceMatcher.Match(Zero(), new[] { Candidate(1, Axis(0, 0.5f)) }, 0.5);
            Assert.AreEqual(MatchOutcome.Matched, result.Outcome);
        }

        [TestMethod]
        public void Match_IsAmbiguous_WhenTheSecondBestStudentIsWithinTheMargin()
        {
            // 0.30 vs 0.33: both under threshold, but only 0.03 apart (< 0.05 margin).
            var result = FaceMatcher.Match(Zero(), new[]
            {
                Candidate(1, Axis(0, 0.30f)),
                Candidate(2, Axis(1, 0.33f))
            }, Threshold);

            Assert.AreEqual(MatchOutcome.Ambiguous, result.Outcome);
            Assert.IsFalse(result.IsMatch);
            Assert.AreEqual(1, result.Best.StudentId);
        }

        [TestMethod]
        public void Match_MarginApplies_EvenWhenTheSecondStudentIsAboveThreshold()
        {
            // Best 0.48 (under 0.5), second 0.51: margin 0.03 is too small, so no match.
            var result = FaceMatcher.Match(Zero(), new[]
            {
                Candidate(1, Axis(0, 0.48f)),
                Candidate(2, Axis(1, 0.51f))
            }, Threshold);

            Assert.AreEqual(MatchOutcome.Ambiguous, result.Outcome);
        }

        [TestMethod]
        public void Match_WithASingleCandidate_NeedsNoMargin()
        {
            var result = FaceMatcher.Match(Zero(), new[] { Candidate(7, Axis(0, 0.4f)) }, Threshold);

            Assert.AreEqual(MatchOutcome.Matched, result.Outcome);
            Assert.AreEqual(7, result.Best.StudentId);
            Assert.IsNull(result.SecondBestDistance);
        }

        [TestMethod]
        public void Match_UsesEachStudentsClosestSample()
        {
            // Student 1 has one far and one near sample; student 2 has a single medium sample.
            var result = FaceMatcher.Match(Zero(), new[]
            {
                Candidate(1, Axis(0, 0.9f), Axis(1, 0.2f)),
                Candidate(2, Axis(2, 0.4f))
            }, Threshold);

            Assert.AreEqual(MatchOutcome.Matched, result.Outcome);
            Assert.AreEqual(1, result.Best.StudentId);
            Assert.AreEqual(0.2, result.BestDistance, Tolerance);
            Assert.AreEqual(0.4, result.SecondBestDistance.Value, Tolerance);
        }

        [TestMethod]
        public void Match_SecondBestIsAnotherStudent_NotAnotherSampleOfTheSameStudent()
        {
            // Two near-identical samples of student 1 must not make the match "ambiguous".
            var result = FaceMatcher.Match(Zero(), new[]
            {
                Candidate(1, Axis(0, 0.20f), Axis(1, 0.21f)),
                Candidate(2, Axis(2, 0.60f))
            }, Threshold);

            Assert.AreEqual(MatchOutcome.Matched, result.Outcome);
            Assert.AreEqual(0.60, result.SecondBestDistance.Value, Tolerance);
        }

        [TestMethod]
        public void Match_WithNoCandidates_ReturnsNoCandidates()
        {
            Assert.AreEqual(MatchOutcome.NoCandidates, FaceMatcher.Match(Zero(), new FaceCandidate[0], Threshold).Outcome);
            Assert.AreEqual(MatchOutcome.NoCandidates, FaceMatcher.Match(Zero(), null, Threshold).Outcome);
            Assert.AreEqual(MatchOutcome.NoCandidates,
                FaceMatcher.Match(Zero(), new[] { Candidate(1 /* no samples */) }, Threshold).Outcome);
        }

        [TestMethod]
        public void Match_StricterThreshold_TurnsAMatchIntoUnknown()
        {
            var candidates = new[] { Candidate(1, Axis(0, 0.42f)) };
            Assert.IsTrue(FaceMatcher.Match(Zero(), candidates, 0.50).IsMatch);
            Assert.IsFalse(FaceMatcher.Match(Zero(), candidates, 0.40).IsMatch);
        }
    }
}
