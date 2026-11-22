using System;
using FaceAttendanceSystem.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FaceAttendanceSystem.Tests.TestVectors;

namespace FaceAttendanceSystem.Tests
{
    [TestClass]
    public class FaceDescriptorTests
    {
        private static string ToBase64(float[] values)
        {
            var bytes = new byte[values.Length * 4];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return Convert.ToBase64String(bytes);
        }

        private static float[] Realistic()
        {
            var rng = new Random(7);
            var v = new float[128];
            for (var i = 0; i < v.Length; i++) v[i] = (float)(rng.NextDouble() * 0.4 - 0.2);
            return v;
        }

        [TestMethod]
        public void Bytes_RoundTrip()
        {
            var original = Realistic();
            var bytes = FaceDescriptor.ToBytes(original);
            Assert.AreEqual(512, bytes.Length);
            CollectionAssert.AreEqual(original, FaceDescriptor.FromBytes(bytes));
        }

        [TestMethod]
        public void TryParseBase64_AcceptsA128ValueDescriptor()
        {
            var original = Realistic();
            float[] parsed;
            string error;

            Assert.IsTrue(FaceDescriptor.TryParseBase64(ToBase64(original), out parsed, out error), error);
            CollectionAssert.AreEqual(original, parsed);
        }

        [TestMethod]
        public void TryParseBase64_RejectsWrongLengths()
        {
            float[] parsed;
            string error;
            Assert.IsFalse(FaceDescriptor.TryParseBase64(ToBase64(new float[127]), out parsed, out error));
            Assert.IsFalse(FaceDescriptor.TryParseBase64(ToBase64(new float[129]), out parsed, out error));
            Assert.IsFalse(FaceDescriptor.TryParseBase64("AAAA", out parsed, out error));
            Assert.IsNull(parsed);
        }

        [TestMethod]
        public void TryParseBase64_RejectsNonFiniteValues()
        {
            foreach (var bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var v = Realistic();
                v[42] = bad;
                float[] parsed;
                string error;
                Assert.IsFalse(FaceDescriptor.TryParseBase64(ToBase64(v), out parsed, out error), "Accepted " + bad);
                StringAssert.Contains(error, "finite");
            }
        }

        [TestMethod]
        public void TryParseBase64_RejectsOutOfRangeAndEmptyDescriptors()
        {
            float[] parsed;
            string error;
            Assert.IsFalse(FaceDescriptor.TryParseBase64(ToBase64(Axis(3, 1000f)), out parsed, out error));
            Assert.IsFalse(FaceDescriptor.TryParseBase64(ToBase64(Zero()), out parsed, out error));
        }

        [TestMethod]
        public void TryParseBase64_RejectsGarbage()
        {
            float[] parsed;
            string error;
            Assert.IsFalse(FaceDescriptor.TryParseBase64(null, out parsed, out error));
            Assert.IsFalse(FaceDescriptor.TryParseBase64("", out parsed, out error));
            Assert.IsFalse(FaceDescriptor.TryParseBase64("not base64 !!", out parsed, out error));
            Assert.IsFalse(FaceDescriptor.TryParseBase64(new string('A', 5000), out parsed, out error));
        }

        [TestMethod]
        public void FromBytes_ReturnsNull_ForCorruptStoredData()
        {
            Assert.IsNull(FaceDescriptor.FromBytes(null));
            Assert.IsNull(FaceDescriptor.FromBytes(new byte[100]));
        }
    }
}
