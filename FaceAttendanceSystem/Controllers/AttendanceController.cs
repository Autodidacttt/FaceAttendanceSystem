using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using FaceAttendanceSystem.Infrastructure;
using FaceAttendanceSystem.Models;
using FaceAttendanceSystem.Services;
using FaceAttendanceSystem.ViewModels;

namespace FaceAttendanceSystem.Controllers
{
    /// <summary>JSON endpoints used by the live attendance page.</summary>
    [RoleAuthorize("Teacher")]
    public class AttendanceController : BaseController
    {
        public const int MaxFacesPerRequest = 10;

        /// <summary>
        /// POST /Attendance/Recognize - matches face descriptors from the camera against the session roster.
        /// Each face comes back as marked / already / unknown (or ambiguous / manual_absent / invalid).
        /// </summary>
        [HttpPost, AjaxValidateAntiForgeryToken]
        public ActionResult Recognize(RecognizeRequest request)
        {
            if (request == null || request.Faces == null || request.Faces.Count == 0)
                return JsonError(400, "No faces were sent.");
            if (request.Faces.Count > MaxFacesPerRequest)
                return JsonError(400, $"Send at most {MaxFacesPerRequest} faces at a time.");

            var session = LoadOwnSession(request.SessionId);
            if (session == null) return JsonError(404, "Session not found.");
            if (!AttendanceRules.CanMarkByFace(session))
                return JsonError(409, "This session has ended.", new { error = "session_ended", message = "This session has ended." });

            var results = new AttendanceService(Db).Recognize(session, request.Faces, CurrentUserId,
                request.SaveSnapshots, Server.MapPath("~/App_Data"));

            var marked = results.Where(r => r.Status == FaceMarkStatus.Marked).ToList();
            if (marked.Count > 0)
            {
                Audit("AttendanceMarkedByFace", "ClassSession", session.Id,
                    string.Join(", ", marked.Select(r => r.Student.RollNumber)));
                Db.SaveChanges();
            }

            return Json(new
            {
                results = results.Select(r => new
                {
                    trackId = r.TrackId,
                    status = StatusText(r.Status),
                    studentId = r.Student?.StudentId,
                    rollNumber = r.Student?.RollNumber,
                    name = r.Student?.FullName,
                    distance = r.Distance,
                    markedAt = r.MarkedUtc.HasValue ? r.MarkedUtc.Value.ToLocalTimeDisplay() : null,
                    error = r.Error
                }),
                presentCount = PresentCount(session.Id)
            });
        }

        /// <summary>POST /Attendance/Override - manual present/absent from the live page.</summary>
        [HttpPost, AjaxValidateAntiForgeryToken]
        public ActionResult Override(OverrideRequest request)
        {
            if (request == null) return JsonError(400, "Invalid request.");

            var session = LoadOwnSession(request.SessionId);
            if (session == null) return JsonError(404, "Session not found.");

            var student = Db.Students.Include("User").FirstOrDefault(s => s.Id == request.StudentId);
            if (student == null) return JsonError(404, "Student not found.");

            var record = new AttendanceService(Db).ManualOverride(session, student.Id, request.Present, CurrentUserId);
            Audit("AttendanceOverride", "ClassSession", session.Id,
                $"{student.RollNumber} marked {(request.Present ? "present" : "absent")} manually");
            Db.SaveChanges();

            return Json(new
            {
                studentId = student.Id,
                rollNumber = student.RollNumber,
                name = student.User.FullName,
                status = request.Present ? "present" : "absent",
                markedAt = record.MarkedUtc.ToLocalTimeDisplay(),
                presentCount = PresentCount(session.Id)
            });
        }

        /// <summary>GET /Attendance/Status/{id} - current present list (the live page refreshes with this).</summary>
        [HttpGet]
        public ActionResult Status(int id)
        {
            var session = LoadOwnSession(id);
            if (session == null) return JsonError(404, "Session not found.");

            var present = Db.Attendances
                .Where(a => a.ClassSessionId == id && a.Status == AttendanceStatus.Present)
                .OrderBy(a => a.MarkedUtc)
                .Select(a => new { a.StudentId, a.Student.RollNumber, a.Student.User.FullName, a.MarkedUtc, a.Method })
                .ToList()
                .Select(a => new
                {
                    studentId = a.StudentId,
                    rollNumber = a.RollNumber,
                    name = a.FullName,
                    markedAt = a.MarkedUtc.ToLocalTimeDisplay(),
                    method = a.Method == AttendanceMethod.Face ? "face" : "manual"
                })
                .ToList();

            return Json(new { active = session.EndedUtc == null, present, presentCount = present.Count },
                JsonRequestBehavior.AllowGet);
        }

        private ClassSession LoadOwnSession(int sessionId)
        {
            var userId = CurrentUserId;
            return Db.ClassSessions
                .Include(cs => cs.Subject)
                .FirstOrDefault(cs => cs.Id == sessionId && cs.Teacher.UserId == userId);
        }

        private int PresentCount(int sessionId)
        {
            return Db.Attendances.Count(a => a.ClassSessionId == sessionId && a.Status == AttendanceStatus.Present);
        }

        private static string StatusText(FaceMarkStatus status)
        {
            switch (status)
            {
                case FaceMarkStatus.Marked: return "marked";
                case FaceMarkStatus.Already: return "already";
                case FaceMarkStatus.ManualAbsent: return "manual_absent";
                case FaceMarkStatus.Ambiguous: return "ambiguous";
                case FaceMarkStatus.Invalid: return "invalid";
                default: return "unknown";
            }
        }
    }
}
