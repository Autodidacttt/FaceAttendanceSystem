using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using FaceAttendanceSystem.Infrastructure;
using FaceAttendanceSystem.Models;
using FaceAttendanceSystem.Services;
using FaceAttendanceSystem.ViewModels;

namespace FaceAttendanceSystem.Controllers
{
    /// <summary>
    /// Face enrollment. Admins can enroll any student; teachers can enroll students on the rosters
    /// of subjects assigned to them. Only descriptors (128 numbers per sample) are stored, never images.
    /// </summary>
    [RoleAuthorize("Admin", "Teacher")]
    public class EnrollmentController : BaseController
    {
        public const int SamplesPerEnrollment = 5;
        public const int MaxSamplesPerStudent = 20;
        public const int MaxSamplesPerRequest = 10;
        public const double MinDetectionScore = 0.5;

        /// <summary>New samples closer than this to another student's samples are flagged as a possible duplicate.</summary>
        public const double DuplicateDistance = 0.4;

        public ActionResult Index(int? id, string q, bool missing = false, int page = 1)
        {
            if (id.HasValue) return Enroll(id.Value);

            var now = DateTime.UtcNow;
            var query = AccessibleStudents();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(s => s.RollNumber.Contains(term) || s.User.FullName.Contains(term));
            }
            if (missing) query = query.Where(s => !s.FaceEnrollments.Any());

            return View("Index", new EnrollmentListViewModel
            {
                Query = q,
                OnlyMissing = missing,
                Students = query
                    .OrderBy(s => s.RollNumber)
                    .Select(s => new StudentListItem
                    {
                        Id = s.Id,
                        RollNumber = s.RollNumber,
                        FullName = s.User.FullName,
                        Department = s.Department,
                        Semester = s.Semester,
                        IsActive = s.User.IsActive,
                        IsLockedOut = s.User.LockoutEndUtc != null && s.User.LockoutEndUtc > now,
                        FaceSamples = s.FaceEnrollments.Count()
                    })
                    .ToPaged(page)
            });
        }

        private ActionResult Enroll(int id)
        {
            var model = AccessibleStudents()
                .Where(s => s.Id == id)
                .Select(s => new EnrollPageViewModel
                {
                    StudentId = s.Id,
                    RollNumber = s.RollNumber,
                    FullName = s.User.FullName,
                    Department = s.Department,
                    Semester = s.Semester,
                    IsActive = s.User.IsActive,
                    ExistingSamples = s.FaceEnrollments.Count()
                })
                .FirstOrDefault();

            if (model == null) return StudentMissingOrForbidden(id);

            model.RequiredSamples = SamplesPerEnrollment;
            model.MaxSamples = MaxSamplesPerStudent;
            return View("Enroll", model);
        }

        [HttpPost, AjaxValidateAntiForgeryToken]
        public ActionResult Save(EnrollmentSaveRequest request)
        {
            if (request == null || request.Samples == null || request.Samples.Count == 0)
                return JsonError(400, "No face samples were sent.");

            if (request.Samples.Count > MaxSamplesPerRequest)
                return JsonError(400, $"Send at most {MaxSamplesPerRequest} samples at a time.");

            var student = AccessibleStudents().Include("User").FirstOrDefault(s => s.Id == request.StudentId);
            if (student == null)
            {
                return Db.Students.Any(s => s.Id == request.StudentId)
                    ? JsonError(403, "You can only enroll students on the rosters of your subjects.")
                    : JsonError(404, "Student not found.");
            }

            var descriptors = new List<float[]>();
            for (var i = 0; i < request.Samples.Count; i++)
            {
                var sample = request.Samples[i];
                float[] values;
                string error;
                if (!FaceDescriptor.TryParseBase64(sample.Descriptor, out values, out error))
                    return JsonError(400, $"Sample {i + 1}: {error}");

                if (double.IsNaN(sample.Score) || sample.Score < MinDetectionScore || sample.Score > 1)
                    return JsonError(400, $"Sample {i + 1}: the face was not detected clearly enough.");

                descriptors.Add(values);
            }

            var existing = Db.FaceEnrollments.Count(f => f.StudentId == student.Id);
            if (existing + descriptors.Count > MaxSamplesPerStudent)
            {
                return JsonError(400,
                    $"A student can have at most {MaxSamplesPerStudent} face samples ({existing} already saved). " +
                    "Delete the existing enrollment first to start again.");
            }

            if (!request.Force)
            {
                var duplicate = FindLikelyDuplicate(student.Id, descriptors);
                if (duplicate != null)
                {
                    return JsonError(409, null, new
                    {
                        error = "duplicate_face",
                        duplicate = true,
                        message = $"This face looks very similar to {duplicate.FullName} ({duplicate.RollNumber}), " +
                                  "who is already enrolled. Check that the right student is in front of the camera."
                    });
                }
            }

            var now = DateTime.UtcNow;
            for (var i = 0; i < descriptors.Count; i++)
            {
                Db.FaceEnrollments.Add(new FaceEnrollment
                {
                    StudentId = student.Id,
                    Descriptor = FaceDescriptor.ToBytes(descriptors[i]),
                    DetectionScore = request.Samples[i].Score,
                    CreatedUtc = now,
                    CreatedByUserId = CurrentUserId
                });
            }

            Audit("FaceEnrolled", "Student", student.Id,
                $"{student.RollNumber}: {descriptors.Count} samples added" + (request.Force ? " (duplicate warning overridden)" : ""));
            Db.SaveChanges();
            FaceCandidateCache.Invalidate();

            return Json(new { ok = true, added = descriptors.Count, total = existing + descriptors.Count });
        }

        [HttpGet]
        public ActionResult Count(int id)
        {
            var student = AccessibleStudents().Where(s => s.Id == id)
                .Select(s => new { s.Id, Count = s.FaceEnrollments.Count() }).FirstOrDefault();
            if (student == null) return JsonError(404, "Student not found.");
            return Json(new { studentId = student.Id, count = student.Count }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var student = AccessibleStudents().FirstOrDefault(s => s.Id == id);
            if (student == null) return StudentMissingOrForbidden(id);

            var samples = Db.FaceEnrollments.Where(f => f.StudentId == id).ToList();
            Db.FaceEnrollments.RemoveRange(samples);
            Audit("FaceEnrollmentDeleted", "Student", id, $"{student.RollNumber}: {samples.Count} samples removed");
            Db.SaveChanges();
            FaceCandidateCache.Invalidate();

            Flash($"Removed {samples.Count} face sample(s) for {student.RollNumber}.");
            return RedirectToAction("Index", new { id });
        }

        /// <summary>Students the current user may enroll.</summary>
        private IQueryable<Student> AccessibleStudents()
        {
            if (User.IsInRole("Admin")) return Db.Students;

            var teacherId = CurrentTeacherId() ?? 0;
            var classes = Db.TeacherSubjects
                .Where(ts => ts.TeacherId == teacherId)
                .Select(ts => new { ts.Subject.Department, ts.Subject.Semester });
            return Db.Students.Where(s => classes.Any(c => c.Department == s.Department && c.Semester == s.Semester));
        }

        private ActionResult StudentMissingOrForbidden(int id)
        {
            return Db.Students.Any(s => s.Id == id) ? ForbiddenResult() : NotFoundResult();
        }

        private FaceCandidate FindLikelyDuplicate(int studentId, List<float[]> newDescriptors)
        {
            var others = Db.FaceEnrollments
                .Where(f => f.StudentId != studentId)
                .Select(f => new { f.StudentId, f.Student.RollNumber, f.Student.User.FullName, f.Descriptor })
                .ToList()
                .GroupBy(f => new { f.StudentId, f.RollNumber, f.FullName })
                .Select(g => new FaceCandidate(g.Key.StudentId, g.Key.RollNumber, g.Key.FullName,
                    g.Select(x => FaceDescriptor.FromBytes(x.Descriptor)).Where(d => d != null).ToList()));

            FaceCandidate closest = null;
            var closestDistance = double.PositiveInfinity;
            foreach (var candidate in others)
            {
                foreach (var descriptor in newDescriptors)
                {
                    var d = FaceMatcher.DistanceToCandidate(descriptor, candidate);
                    if (d < closestDistance)
                    {
                        closestDistance = d;
                        closest = candidate;
                    }
                }
            }

            return closestDistance < DuplicateDistance ? closest : null;
        }
    }
}
