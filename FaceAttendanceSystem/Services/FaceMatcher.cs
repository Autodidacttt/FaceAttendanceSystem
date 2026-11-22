using System;
using System.Collections.Generic;

namespace FaceAttendanceSystem.Services
{
    public enum MatchOutcome
    {
        Matched,
        NoCandidates,
        AboveThreshold,
        Ambiguous
    }

    public sealed class MatchResult
    {
        public MatchOutcome Outcome { get; set; }
        public bool IsMatch => Outcome == MatchOutcome.Matched;

        /// <summary>Closest student (set even when the match is rejected, for diagnostics).</summary>
        public FaceCandidate Best { get; set; }
        public double BestDistance { get; set; } = double.PositiveInfinity;
        public double? SecondBestDistance { get; set; }
    }

    /// <summary>
    /// Matches a face descriptor against enrolled students using Euclidean distance.
    /// A student's distance is the smallest distance to any of their samples.
    /// A match needs: best distance &lt;= threshold AND (second-best student - best) &gt;= margin.
    /// </summary>
    public static class FaceMatcher
    {
        public const double DefaultMargin = 0.05;

        public static double Distance(float[] a, float[] b)
        {
            if (a == null || b == null) throw new ArgumentNullException(a == null ? nameof(a) : nameof(b));
            if (a.Length != b.Length) throw new ArgumentException("Descriptors must have the same length.");

            double sum = 0;
            for (var i = 0; i < a.Length; i++)
            {
                double d = a[i] - b[i];
                sum += d * d;
            }
            return Math.Sqrt(sum);
        }

        public static double DistanceToCandidate(float[] descriptor, FaceCandidate candidate)
        {
            var best = double.PositiveInfinity;
            foreach (var sample in candidate.Descriptors)
            {
                var d = Distance(descriptor, sample);
                if (d < best) best = d;
            }
            return best;
        }

        public static MatchResult Match(float[] descriptor, IEnumerable<FaceCandidate> candidates, double threshold,
            double margin = DefaultMargin)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));

            FaceCandidate best = null;
            var bestDistance = double.PositiveInfinity;
            var secondDistance = double.PositiveInfinity;

            if (candidates != null)
            {
                foreach (var candidate in candidates)
                {
                    if (candidate.Descriptors.Count == 0) continue;

                    var d = DistanceToCandidate(descriptor, candidate);
                    if (d < bestDistance)
                    {
                        secondDistance = bestDistance;
                        bestDistance = d;
                        best = candidate;
                    }
                    else if (d < secondDistance)
                    {
                        secondDistance = d;
                    }
                }
            }

            var result = new MatchResult
            {
                Best = best,
                BestDistance = bestDistance,
                SecondBestDistance = double.IsPositiveInfinity(secondDistance) ? (double?)null : secondDistance
            };

            if (best == null)
                result.Outcome = MatchOutcome.NoCandidates;
            else if (bestDistance > threshold)
                result.Outcome = MatchOutcome.AboveThreshold;
            else if (result.SecondBestDistance.HasValue && result.SecondBestDistance.Value - bestDistance < margin)
                result.Outcome = MatchOutcome.Ambiguous;
            else
                result.Outcome = MatchOutcome.Matched;

            return result;
        }
    }
}
