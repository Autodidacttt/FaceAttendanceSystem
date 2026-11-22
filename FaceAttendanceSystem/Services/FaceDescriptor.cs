using System;
using System.Collections.Generic;

namespace FaceAttendanceSystem.Services
{
    /// <summary>
    /// A face-api.js face descriptor: 128 float32 values. On the wire it is the base64 of the
    /// Float32Array's bytes (little-endian); in the database it is the same 512 bytes.
    /// </summary>
    public static class FaceDescriptor
    {
        public const int Length = 128;
        public const int ByteLength = Length * sizeof(float);

        /// <summary>Generous bound; real face-api.js descriptor values are well inside [-1, 1].</summary>
        public const float MaxAbsValue = 10f;

        public static bool TryParseBase64(string base64, out float[] descriptor, out string error)
        {
            descriptor = null;
            error = null;

            if (string.IsNullOrWhiteSpace(base64))
            {
                error = "Descriptor is missing.";
                return false;
            }

            // 512 bytes encode to 684 base64 characters.
            if (base64.Length > 700)
            {
                error = "Descriptor is too long.";
                return false;
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                error = "Descriptor is not valid base64.";
                return false;
            }

            if (bytes.Length != ByteLength)
            {
                error = $"Descriptor must contain exactly {Length} values.";
                return false;
            }

            var values = FromBytes(bytes);
            if (!IsValid(values, out error)) return false;

            descriptor = values;
            return true;
        }

        public static bool IsValid(float[] values, out string error)
        {
            error = null;
            if (values == null || values.Length != Length)
            {
                error = $"Descriptor must contain exactly {Length} values.";
                return false;
            }

            var allZero = true;
            foreach (var v in values)
            {
                if (float.IsNaN(v) || float.IsInfinity(v))
                {
                    error = "Descriptor contains a value that is not a finite number.";
                    return false;
                }
                if (Math.Abs(v) > MaxAbsValue)
                {
                    error = "Descriptor contains an out-of-range value.";
                    return false;
                }
                if (v != 0f) allZero = false;
            }

            if (allZero)
            {
                error = "Descriptor is empty.";
                return false;
            }
            return true;
        }

        public static float[] FromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length != ByteLength) return null;

            var values = new float[Length];
            if (BitConverter.IsLittleEndian)
            {
                Buffer.BlockCopy(bytes, 0, values, 0, ByteLength);
            }
            else
            {
                for (var i = 0; i < Length; i++)
                {
                    var chunk = new byte[4];
                    Array.Copy(bytes, i * 4, chunk, 0, 4);
                    Array.Reverse(chunk);
                    values[i] = BitConverter.ToSingle(chunk, 0);
                }
            }
            return values;
        }

        public static byte[] ToBytes(float[] values)
        {
            if (values == null || values.Length != Length) throw new ArgumentException("Descriptor must have 128 values.", nameof(values));

            var bytes = new byte[ByteLength];
            if (BitConverter.IsLittleEndian)
            {
                Buffer.BlockCopy(values, 0, bytes, 0, ByteLength);
            }
            else
            {
                for (var i = 0; i < Length; i++)
                {
                    var chunk = BitConverter.GetBytes(values[i]);
                    Array.Reverse(chunk);
                    Array.Copy(chunk, 0, bytes, i * 4, 4);
                }
            }
            return bytes;
        }
    }

    /// <summary>A student who can be recognised, with all of their enrolled descriptors.</summary>
    public sealed class FaceCandidate
    {
        public FaceCandidate(int studentId, string rollNumber, string fullName, IReadOnlyList<float[]> descriptors)
        {
            StudentId = studentId;
            RollNumber = rollNumber;
            FullName = fullName;
            Descriptors = descriptors ?? new List<float[]>();
        }

        public int StudentId { get; }
        public string RollNumber { get; }
        public string FullName { get; }
        public IReadOnlyList<float[]> Descriptors { get; }
    }
}
