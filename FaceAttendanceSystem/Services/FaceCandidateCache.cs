using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Threading;
using FaceAttendanceSystem.Data;

namespace FaceAttendanceSystem.Services
{
    /// <summary>
    /// Caches, per class session, the face descriptors of the students on that session's roster,
    /// so recognition requests don't reload every descriptor from the database.
    /// Any change to enrollments or student records bumps a global version, which invalidates every entry.
    /// </summary>
    public static class FaceCandidateCache
    {
        private static readonly MemoryCache Cache = new MemoryCache("FaceCandidates");
        private static long _version;

        public static void Invalidate()
        {
            Interlocked.Increment(ref _version);
        }

        public static IReadOnlyList<FaceCandidate> ForSession(AppDbContext db, int sessionId, string department, int semester)
        {
            var key = "session:" + sessionId + ":v" + Interlocked.Read(ref _version);
            var cached = Cache.Get(key) as IReadOnlyList<FaceCandidate>;
            if (cached != null) return cached;

            var rows = RosterService.RosterFor(db, department, semester)
                .Select(s => new
                {
                    s.Id,
                    s.RollNumber,
                    s.User.FullName,
                    Descriptors = s.FaceEnrollments.Select(f => f.Descriptor)
                })
                .ToList();

            var candidates = rows
                .Select(r => new FaceCandidate(
                    r.Id,
                    r.RollNumber,
                    r.FullName,
                    r.Descriptors.Select(FaceDescriptor.FromBytes).Where(d => d != null).ToList()))
                .Where(c => c.Descriptors.Count > 0)
                .ToList();

            Cache.Set(key, candidates, new CacheItemPolicy { SlidingExpiration = TimeSpan.FromMinutes(30) });
            return candidates;
        }
    }
}
