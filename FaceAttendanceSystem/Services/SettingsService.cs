using System;
using System.Globalization;
using System.Linq;
using FaceAttendanceSystem.Data;
using FaceAttendanceSystem.Models;

namespace FaceAttendanceSystem.Services
{
    /// <summary>Reads and writes application settings (currently the face match threshold).</summary>
    public static class SettingsService
    {
        public const double DefaultMatchThreshold = 0.5;
        public const double MinMatchThreshold = 0.35;
        public const double MaxMatchThreshold = 0.60;

        private static readonly object Sync = new object();
        private static double? _cachedThreshold;

        public static double GetMatchThreshold(AppDbContext db)
        {
            var cached = _cachedThreshold;
            if (cached.HasValue) return cached.Value;

            var raw = db.AppSettings.Where(s => s.Key == AppSetting.MatchThresholdKey).Select(s => s.Value).FirstOrDefault();
            double value;
            if (raw == null || !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                value < MinMatchThreshold || value > MaxMatchThreshold)
            {
                value = DefaultMatchThreshold;
            }

            lock (Sync) _cachedThreshold = value;
            return value;
        }

        public static void SetMatchThreshold(AppDbContext db, double value)
        {
            if (double.IsNaN(value) || value < MinMatchThreshold || value > MaxMatchThreshold)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            var setting = db.AppSettings.Find(AppSetting.MatchThresholdKey);
            var text = value.ToString("0.00", CultureInfo.InvariantCulture);
            if (setting == null)
            {
                db.AppSettings.Add(new AppSetting { Key = AppSetting.MatchThresholdKey, Value = text });
            }
            else
            {
                setting.Value = text;
            }
        }

        /// <summary>Call after SaveChanges so the next read picks up the new value.</summary>
        public static void InvalidateCache()
        {
            lock (Sync) _cachedThreshold = null;
        }
    }
}
