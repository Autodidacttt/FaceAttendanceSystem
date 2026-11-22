using System.ComponentModel.DataAnnotations;

namespace FaceAttendanceSystem.Models
{
    /// <summary>Simple key/value settings table (e.g. the face match threshold).</summary>
    public class AppSetting
    {
        public const string MatchThresholdKey = "MatchThreshold";

        [Key, StringLength(100)]
        public string Key { get; set; }

        [Required, StringLength(500)]
        public string Value { get; set; }
    }
}
