using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using FaceAttendanceSystem.Data;
using FaceAttendanceSystem.Infrastructure;
using FaceAttendanceSystem.Models;
using FaceAttendanceSystem.ViewModels;

namespace FaceAttendanceSystem.Services
{
    public enum FaceMarkStatus
    {
        Marked,
        Already,
        ManualAbsent,
        Unknown,
        Ambiguous,
        Invalid
    }

    public sealed class FaceMarkResult
    {
        public string TrackId { get; set; }
        public FaceMarkStatus Status { get; set; }
        public FaceCandidate Student { get; set; }
        public double? Distance { get; set; }
        public DateTime? MarkedUtc { get; set; }
        public string Error { get; set; }
    }

    /// <summary>Marks attendance from recognised faces and from manual overrides.</summary>
    public class AttendanceService
    {
        public const int MaxSnapshotBytes = 150 * 1024;
        private const string JpegDataUrlPrefix = "data:image/jpeg;base64,";

        private readonly AppDbContext _db;

        public AttendanceService(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Matches each face against the session roster and records Present for new matches.
        /// </summary>
        /// <param name="appDataPath">Physical App_Data path; snapshots are written below it when enabled.</param>
        public List<FaceMarkResult> Recognize(ClassSession session, IEnumerable<RecognizeFace> faces, int userId,
            bool saveSnapshots, string appDataPath)
        {
            var candidates = FaceCandidateCache.ForSession(_db, session.Id, session.Subject.Department, session.Subject.Semester);
            var threshold = SettingsService.GetMatchThreshold(_db);
            var results = new List<FaceMarkResult>();
            var markedThisRequest = new HashSet<int>();

            foreach (var face in faces)
            {
                var result = new FaceMarkResult { TrackId = Truncate(face.TrackId, 40) };
                results.Add(result);

                float[] descriptor;
                string error;
                if (!FaceDescriptor.TryParseBase64(face.Descriptor, out descriptor, out error))
                {
                    result.Status = FaceMarkStatus.Invalid;
                    result.Error = error;
                    continue;
                }

                var match = FaceMatcher.Match(descriptor, candidates, threshold);
                if (!match.IsMatch)
                {
                    result.Status = match.Outcome == MatchOutcome.Ambiguous ? FaceMarkStatus.Ambiguous : FaceMarkStatus.Unknown;
                    continue;
                }

                result.Student = match.Best;
                result.Distance = Math.Round(match.BestDistance, 4);

                // Two faces in one frame matching the same student: only the first counts.
                if (!markedThisRequest.Add(match.Best.StudentId))
                {
                    result.Status = FaceMarkStatus.Already;
                    continue;
                }

                byte[] snapshot = null;
                if (saveSnapshots) TryDecodeSnapshot(face.Snapshot, out snapshot);

                MarkByFace(session, match.Best.StudentId, match.BestDistance, userId, snapshot, appDataPath, result);
            }

            return results;
        }

        private void MarkByFace(ClassSession session, int studentId, double distance, int userId, byte[] snapshot,
            string appDataPath, FaceMarkResult result)
        {
            var existing = _db.Attendances.FirstOrDefault(a => a.ClassSessionId == session.Id && a.StudentId == studentId);

            switch (AttendanceRules.DecideFaceMark(existing))
            {
                case FaceMarkDecision.AlreadyPresent:
                    result.Status = FaceMarkStatus.Already;
                    result.MarkedUtc = existing.MarkedUtc;
                    return;
                case FaceMarkDecision.KeepManualAbsent:
                    result.Status = FaceMarkStatus.ManualAbsent;
                    return;
            }

            var now = DateTime.UtcNow;
            var record = existing ?? new Attendance { ClassSessionId = session.Id, StudentId = studentId };
            record.Status = AttendanceStatus.Present;
            record.Method = AttendanceMethod.Face;
            record.MarkedUtc = now;
            record.MatchDistance = Math.Round(distance, 4);
            record.MarkedByUserId = userId;
            if (existing == null) _db.Attendances.Add(record);

            try
            {
                _db.SaveChanges();
            }
            catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
            {
                // Another request marked this student a moment ago.
                _db.Entry(record).State = EntityState.Detached;
                result.Status = FaceMarkStatus.Already;
                return;
            }

            result.Status = FaceMarkStatus.Marked;
            result.MarkedUtc = now;

            if (snapshot != null && !string.IsNullOrEmpty(appDataPath))
            {
                try
                {
                    var relative = Path.Combine("snapshots", session.Id.ToString(), studentId + ".jpg");
                    var full = Path.Combine(appDataPath, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(full));
                    File.WriteAllBytes(full, snapshot);
                    record.SnapshotPath = relative.Replace('\\', '/');
                    _db.SaveChanges();
                }
                catch (IOException)
                {
                    // A snapshot is optional; attendance is already recorded.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        /// <summary>Teacher/admin decision: present or absent. Always wins over face recognition.</summary>
        public Attendance ManualOverride(ClassSession session, int studentId, bool present, int userId)
        {
            var record = _db.Attendances.FirstOrDefault(a => a.ClassSessionId == session.Id && a.StudentId == studentId);
            if (record == null)
            {
                record = new Attendance { ClassSessionId = session.Id, StudentId = studentId };
                _db.Attendances.Add(record);
            }

            record.Status = present ? AttendanceStatus.Present : AttendanceStatus.Absent;
            record.Method = AttendanceMethod.Manual;
            record.MarkedUtc = DateTime.UtcNow;
            record.MarkedByUserId = userId;
            if (!present) record.MatchDistance = null;
            return record;
        }

        /// <summary>Roster (active students of the subject's class) plus anyone with a record in the session.</summary>
        public List<RosterEntry> GetRosterStatus(ClassSession session)
        {
            var roster = RosterService.RosterFor(_db, session.Subject)
                .Select(s => new RosterEntry
                {
                    StudentId = s.Id,
                    RollNumber = s.RollNumber,
                    FullName = s.User.FullName,
                    HasFace = s.FaceEnrollments.Any()
                })
                .ToList();

            var records = _db.Attendances
                .Where(a => a.ClassSessionId == session.Id)
                .Select(a => new
                {
                    a.Id,
                    a.StudentId,
                    a.Status,
                    a.Method,
                    a.MarkedUtc,
                    a.MatchDistance,
                    a.SnapshotPath,
                    a.Student.RollNumber,
                    a.Student.User.FullName,
                    HasFace = a.Student.FaceEnrollments.Any()
                })
                .ToList();

            var byStudent = roster.ToDictionary(r => r.StudentId);
            foreach (var rec in records)
            {
                RosterEntry entry;
                if (!byStudent.TryGetValue(rec.StudentId, out entry))
                {
                    entry = new RosterEntry
                    {
                        StudentId = rec.StudentId,
                        RollNumber = rec.RollNumber,
                        FullName = rec.FullName,
                        HasFace = rec.HasFace,
                        OnRoster = false
                    };
                    roster.Add(entry);
                    byStudent[rec.StudentId] = entry;
                }

                entry.AttendanceId = rec.Id;
                entry.Status = rec.Status;
                entry.Method = rec.Method;
                entry.MarkedUtc = rec.MarkedUtc;
                entry.Distance = rec.MatchDistance;
                entry.HasSnapshot = !string.IsNullOrEmpty(rec.SnapshotPath);
            }

            return roster.OrderBy(r => r.RollNumber).ToList();
        }

        public static bool TryDecodeSnapshot(string dataUrl, out byte[] jpeg)
        {
            jpeg = null;
            if (string.IsNullOrEmpty(dataUrl) || !dataUrl.StartsWith(JpegDataUrlPrefix, StringComparison.Ordinal)) return false;

            var base64 = dataUrl.Substring(JpegDataUrlPrefix.Length);
            if (base64.Length > MaxSnapshotBytes * 4 / 3 + 4) return false;

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                return false;
            }

            // JPEG files start with FF D8 FF.
            if (bytes.Length < 4 || bytes.Length > MaxSnapshotBytes || bytes[0] != 0xFF || bytes[1] != 0xD8 || bytes[2] != 0xFF)
                return false;

            jpeg = bytes;
            return true;
        }

        private static string Truncate(string value, int max)
        {
            if (value == null) return null;
            return value.Length <= max ? value : value.Substring(0, max);
        }
    }
}
