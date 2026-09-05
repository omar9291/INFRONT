using System;
using System.Collections.Generic;

namespace Infront
{
    /// <summary>Bildzeiten bleiben Bildzeiten: erst mitteln, dann in FPS umrechnen.
    /// Der 1%-Tiefpunkt ist der Kehrwert der mittleren langsamsten 1% Bildzeiten.</summary>
    public static class FrameStatistics
    {
        [Serializable]
        public struct Result
        {
            public int frames;
            public double seconds;
            public double averageFps;
            public double onePercentLowFps;
            public double p99Milliseconds;
            public double maxMilliseconds;
            public int framesOver20Milliseconds;
        }

        public static Result Calculate(IReadOnlyList<float> seconds)
        {
            if (seconds == null) throw new ArgumentNullException(nameof(seconds));
            var sorted = new float[seconds.Count];
            int n = 0;
            double total = 0;
            int over = 0;
            for (int i = 0; i < seconds.Count; i++)
            {
                float t = seconds[i];
                if (t <= 0 || float.IsNaN(t) || float.IsInfinity(t)) continue;
                sorted[n++] = t;
                total += t;
                if (t > 0.020f) over++;
            }
            if (n == 0) return default;
            Array.Sort(sorted, 0, n);
            int worstCount = Math.Max(1, (int)Math.Ceiling(n * 0.01));
            double worstSeconds = 0;
            for (int i = n - worstCount; i < n; i++) worstSeconds += sorted[i];
            return new Result
            {
                frames = n, seconds = total, averageFps = n / total,
                onePercentLowFps = worstCount / worstSeconds,
                p99Milliseconds = sorted[Math.Max(0, (int)Math.Ceiling(n * 0.99) - 1)] * 1000.0,
                maxMilliseconds = sorted[n - 1] * 1000.0,
                framesOver20Milliseconds = over
            };
        }
    }
}
