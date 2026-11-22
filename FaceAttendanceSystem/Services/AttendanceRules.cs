using FaceAttendanceSystem.Models;

namespace FaceAttendanceSystem.Services
{
    public enum FaceMarkDecision
    {
        /// <summary>No record yet: create a Present record.</summary>
        CreatePresent,

        /// <summary>Already present in this session: do nothing (duplicate).</summary>
        AlreadyPresent,

        /// <summary>The teacher manually marked the student absent: face recognition does not override that.</summary>
        KeepManualAbsent
    }

    /// <summary>
    /// The duplicate-attendance rule, kept free of EF so it can be unit tested.
    /// A student has at most one attendance row per session; recognising them again never creates another,
    /// and an explicit manual "absent" decision by the teacher wins over the camera.
    /// </summary>
    public static class AttendanceRules
    {
        public static FaceMarkDecision DecideFaceMark(Attendance existing)
        {
            if (existing == null) return FaceMarkDecision.CreatePresent;
            if (existing.Status == AttendanceStatus.Present) return FaceMarkDecision.AlreadyPresent;
            return existing.Method == AttendanceMethod.Manual
                ? FaceMarkDecision.KeepManualAbsent
                : FaceMarkDecision.CreatePresent;
        }

        /// <summary>A face can only mark attendance in a session that is still running.</summary>
        public static bool CanMarkByFace(ClassSession session)
        {
            return session != null && session.EndedUtc == null;
        }
    }
}
