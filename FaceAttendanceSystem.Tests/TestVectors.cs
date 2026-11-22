using System.Collections.Generic;
using FaceAttendanceSystem.Services;

namespace FaceAttendanceSystem.Tests
{
    /// <summary>Helpers to build 128-value descriptors with easy-to-reason-about distances.</summary>
    internal static class TestVectors
    {
        public static float[] Zero()
        {
            return new float[FaceDescriptor.Length];
        }

        /// <summary>A vector that is <paramref name="value"/> on one axis, so its distance from Zero() is |value|.</summary>
        public static float[] Axis(int index, float value)
        {
            var v = Zero();
            v[index] = value;
            return v;
        }

        public static float[] Filled(float value)
        {
            var v = Zero();
            for (var i = 0; i < v.Length; i++) v[i] = value;
            return v;
        }

        public static FaceCandidate Candidate(int id, params float[][] descriptors)
        {
            return new FaceCandidate(id, "S" + id.ToString("000"), "Student " + id, new List<float[]>(descriptors));
        }
    }
}
