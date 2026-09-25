using System;
using System.Collections.Generic;
using System.Linq;

namespace RhythmHunter.RhythmDemo
{
    /// <summary>Robust, uncorrected tap statistics; never averages already compensated inputs.</summary>
    public sealed class RhythmTimingSamples
    {
        public const int TargetCount = 32;
        readonly List<double> values = new();
        readonly HashSet<long> beats = new();
        int warmup;
        public int Count => values.Count;
        public int WarmupRemaining => 4 - warmup;
        public bool Complete => Count >= TargetCount;
        public double MedianMs { get; private set; }
        public double SpreadMs { get; private set; }
        public double DriftMs { get; private set; }
        public int InlierCount { get; private set; }
        public bool Reliable => Complete && InlierCount >= 24 && SpreadMs <= 30 &&
            Math.Abs(DriftMs) <= 30 && Math.Abs(MedianMs) <= FmodRhythmJudge.MaxPersonalDelayMs;

        public void Clear()
        {
            values.Clear(); beats.Clear(); warmup = 0;
            MedianMs = SpreadMs = DriftMs = 0; InlierCount = 0;
        }

        public bool Add(long beat, double deltaMs, double intervalMs)
        {
            if (Complete || beat < 0 || double.IsNaN(deltaMs) || double.IsInfinity(deltaMs) ||
                intervalMs <= 0 || Math.Abs(deltaMs) > Math.Min(200, intervalMs * .45) || !beats.Add(beat)) return false;
            if (warmup < 4) { warmup++; return true; }
            values.Add(deltaMs);
            double center = Median(values);
            double mad = Median(values.Select(v => Math.Abs(v - center)));
            var inliers = values.Where(v => Math.Abs(v - center) <= Math.Max(12, 3 * 1.4826 * mad)).ToArray();
            InlierCount = inliers.Length;
            MedianMs = Median(inliers);
            SpreadMs = 1.4826 * Median(inliers.Select(v => Math.Abs(v - MedianMs)));
            if (Count >= 16) DriftMs = Median(values.Skip(Count - 8)) - Median(values.Take(8));
            return true;
        }

        static double Median(IEnumerable<double> source)
        {
            var sorted = source.OrderBy(v => v).ToArray();
            if (sorted.Length == 0) return 0;
            int middle = sorted.Length / 2;
            return sorted.Length % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) * .5 : sorted[middle];
        }
    }
}
