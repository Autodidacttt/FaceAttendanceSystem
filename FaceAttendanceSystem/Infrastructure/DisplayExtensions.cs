using System;
using System.Globalization;

namespace FaceAttendanceSystem.Infrastructure
{
    /// <summary>Formatting helpers for views. All dates are stored in UTC and shown in server local time.</summary>
    public static class DisplayExtensions
    {
        public static DateTime ToLocal(this DateTime utc)
        {
            return DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
        }

        public static string ToLocalDisplay(this DateTime utc)
        {
            return utc.ToLocal().ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);
        }

        public static string ToLocalDisplay(this DateTime? utc)
        {
            return utc.HasValue ? utc.Value.ToLocalDisplay() : "-";
        }

        public static string ToLocalTimeDisplay(this DateTime utc)
        {
            return utc.ToLocal().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }

        public static string Percent(double value)
        {
            return value.ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>UTC range [start, end) covering the given local calendar date.</summary>
        public static void LocalDayToUtcRange(DateTime localDate, out DateTime startUtc, out DateTime endUtc)
        {
            var start = DateTime.SpecifyKind(localDate.Date, DateTimeKind.Local);
            startUtc = start.ToUniversalTime();
            endUtc = start.AddDays(1).ToUniversalTime();
        }
    }
}
